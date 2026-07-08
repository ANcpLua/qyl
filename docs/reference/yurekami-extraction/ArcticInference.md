# ArcticInference

**One-liner:** Snowflake's open-source vLLM plugin that hot-patches a stock vLLM install (no fork) to add Arctic Ulysses/Shift sequence-parallelism, LSTM/MLP + suffix-tree speculative decoding, SwiftKV model surgery, Dynasor reasoning early-exit, and a multi-replica embedding server — plus a handful of custom CUDA kernels.

**Stack / language:** Python 3.10+ (~13.9k LOC) targeting `vllm==0.11.0` on CUDA; C++/CUDA custom ops (~1.3k LOC) built via CMake + nanobind; a native C++ suffix-tree extension (`suffix_decoding._C`); gRPC/protobuf for the embedding replica manager. Distributed as a PyPI package `arctic-inference` that registers itself through vLLM's `vllm.general_plugins` entry point.

---

## 1. Architecture overview

ArcticInference is architecturally unusual: it ships **almost no code that vLLM calls directly**. Instead it registers a single entry point (`arctic_inference.vllm.plugin:arctic_inference_plugin`) under vLLM's `vllm.general_plugins` group. When `ARCTIC_INFERENCE_ENABLED=1`, vLLM loads the plugin, which:

1. Version-checks vLLM and confirms the CUDA platform.
2. Registers custom model architectures (SwiftKV Llama, Arctic MLP/LSTM speculators) into vLLM's `ModelRegistry` and `transformers.AutoConfig`.
3. Applies a cascade of **monkey-patches** to vLLM internals via the `ArcticPatch` mechanism (see §4.1).

The patch targets, in dependency order:

- **Args/config layer** (`args.py`, `config.py`): extends `EngineArgs`/`ParallelConfig`/`SpeculativeConfig` with Arctic-specific fields (`ulysses_sequence_parallel_size`, `enable_shift_parallel`, `shift_parallel_threshold`) using a `__new__`-swap trick so `EngineArgs(...)` transparently returns an `ArcticEngineArgs`.
- **Parallelism layer** (`ulysses.py`): rewrites `initialize_model_parallel` to construct an extra `_SP` (sequence-parallel) group and a `_SP_TP` (full-TP "shift") group on top of vLLM's DP×PP×TP grid, and patches `Attention.forward` to do the Ulysses all-to-all.
- **Execution layer** (`model_runner.py`, 1137 LOC): patches `GPUModelRunner` to route each forward batch between a *sequence-parallel* model and a *shift-parallel (full-TP)* model depending on batch size (`shift_parallel_threshold`) — the core "Shift Parallelism" idea: use SP for large prefill batches, switch to TP for small decode batches, sharing weights.
- **Speculative decoding** (`spec_dec/`, `suffix_decoding/`): Arctic MLP/LSTM speculator models + a C++ suffix-tree draft engine.
- **Model optimization** (`swiftkv/`): a SwiftKV Llama that computes KV projections for later layers from an early layer's hidden state.

Independent side-projects live under their own packages: `dynasor/` (reasoning-model early-exit via answer-entropy probing) and `embedding/` (a gRPC load-balancing replica manager that packs multiple embedding replicas onto one GPU). These do not require the vLLM patch machinery.

---

## 2. File-by-file / module map

### Core plugin & patch framework
| Path | What |
|---|---|
| `arctic_inference/patching.py` | The `ArcticPatch[Target]` base class — the whole monkey-patch framework (subscription syntax, collision detection, classmethod rebind). ~140 LOC, high reuse value. |
| `arctic_inference/vllm/plugin.py` | Entry-point function; gates on env + version + CUDA, then calls `apply_arctic_patches()`. |
| `arctic_inference/vllm/patches.py` | Master patch orchestrator: registers models, applies every `*Patch` in the right order. Also patches `EngineCoreProc.run_engine_core` (reload plugins in spawned proc) and `WorkerBase.__init__` (defer CUDA-importing patches until after fork). |
| `arctic_inference/envs.py` | Env-var flags (`ARCTIC_INFERENCE_ENABLED`, skip-version/platform checks). |
| `arctic_inference/utils.py` | `get_compatible_vllm_version()` etc. |

### vLLM args / config patches
| Path | What |
|---|---|
| `arctic_inference/vllm/args.py` | `ArcticArgs` dataclass + `EngineArgsPatch`/`AsyncEngineArgsPatch`. Uses `__new__` swap so constructing base `EngineArgs` yields the Arctic subclass; adds CLI flags. |
| `arctic_inference/vllm/config.py` | `ParallelConfigPatch`, `SpeculativeConfigPatch`, `VllmConfigPatch`, `MLPSpeculatorConfigPatch` — thread Arctic fields through vLLM config objects, incl. the `"arctic"` speculative method. |
| `arctic_inference/vllm/stats.py` | Patches spec-decoding stats/logging to report acceptance rates. |
| `arctic_inference/vllm/structured_output.py` | `XgrammarBackendPatch` for guided/JSON decoding. |

### Parallelism (Ulysses + Shift)
| Path | What |
|---|---|
| `arctic_inference/vllm/ulysses.py` | (461) All shift-parallel patches. Builds `_SP`/`_SP_TP` process groups; `UlyssesAttention.forward` does the two-phase all-to-all; patches executor/worker/cudagraph-dispatcher for SP-aware batch sizes and cleanup. |
| `arctic_inference/vllm/model_runner.py` | (1137) The biggest file. `GPUModelRunnerPatch` + `set_shift_parallel_mode()` context manager that swaps `_TP`↔`_SP_TP` groups; dual-model (SP vs shift) routing by batch size; integrates speculators + SwiftKV metadata. |

### Speculative decoding
| Path | What |
|---|---|
| `arctic_inference/vllm/spec_dec/arctic_speculator.py` | (968) `ArcticMLPSpeculator` & `ArcticLSTMSpeculator` nn.Modules (token+embedding speculators, tied-weight stages, CUDA-graph capture per (padding,head), optional fused `speculator_ln`/`sum_lstm` custom ops). |
| `arctic_inference/vllm/spec_dec/arctic_proposer.py` | Proposer that drives the speculator inside vLLM's spec-decode loop. |
| `arctic_inference/vllm/spec_dec/vocab_parallel_embedding.py` | (496) TP-aware embedding / `ParallelLMHead` with `SpeculatorTPInit` mixin. |
| `arctic_inference/vllm/spec_dec/fp8.py` | (334) `Fp8ConfigWithEmbedding` + FP8 linear method for a quantized LM head. |
| `arctic_inference/vllm/spec_dec/logits_processor_opt.py` | Optimized logits processor. |
| `arctic_inference/suffix_decoding/cache.py` | (357) `SuffixDecodingCache` — Python wrapper over native `SuffixTree`/`Draft`; global (cross-request) + per-request local suffix trees, FIFO eviction, zero-copy int32 ndarray path, picks best of local/global draft. |
| `arctic_inference/suffix_decoding/simulator.py` | (594) Offline simulator/benchmark harness for suffix decoding over datasets. |

### SwiftKV
| Path | What |
|---|---|
| `arctic_inference/vllm/swiftkv/llama_swiftkv.py` | (869) `LlamaSwiftKVForCausalLM` — Llama variant where later layers' KV are projected from an early layer's hidden state (`q_proj_swiftkv`/`kv_proj_swiftkv`), cutting KV compute. Handles FlashInfer metadata. |
| `arctic_inference/common/swiftkv/configs.py` | `LlamaSwiftKVConfig` HF config (registered to AutoConfig). |

### Custom CUDA ops
| Path | What |
|---|---|
| `arctic_inference/py_custom_ops.py` | Python thin wrappers + `try_load_torch_library()` that dynamically finds & loads the compiled `custom_ops*.so`, gracefully falling back to pure-torch if absent. |
| `arctic_inference/op_builder/builder.py` | (545) DeepSpeed-derived JIT `OpBuilder` (CUDA version detection, compute-capability autodetect, ninja build) for optional runtime compilation. |
| `arctic_inference/op_builder/swiftkv_ops_builder.py` | SwiftKV JIT op spec. |
| `csrc/custom_ops/sum_lstm.cu` | (472) Fused LSTM-cell + layernorm kernel for the LSTM speculator. |
| `csrc/custom_ops/reshape_and_cache_flash_fp4.cu` | (337) NVFP4 KV-cache reshape-and-cache. |
| `csrc/custom_ops/reshape_and_cache_flash_bulk.cu` | (143) Bulk multi-layer reshape-and-cache. |
| `csrc/custom_ops/speculator_ln.cu` | (252) Fused RMS/L2 layernorm returning intermediates for the speculator. |
| `csrc/custom_ops/torch_bindings.cpp` | Torch library registration of the ops. |

### Dynasor (reasoning early-exit)
| Path | What |
|---|---|
| `arctic_inference/dynasor/entropy.py` | (250) Answer-consistency / Shannon-entropy utilities; `should_early_exit()` decides when a reasoning model's repeated answers are confident enough to stop. |
| `arctic_inference/dynasor/evaluator.py` | (852) `math_equal` and heavy math-answer equivalence checking (sympy/latex2sympy). |
| `arctic_inference/dynasor/cot.py` | Chain-of-thought prompt formatting + streaming OpenAI completion helpers. |
| `arctic_inference/dynasor/openai_server.py` / `vllm_server.py` | Proxy servers that inject the probe-and-early-exit loop. |

### Embedding replica server
| Path | What |
|---|---|
| `arctic_inference/embedding/replica_manager.py` | (509) gRPC front server; spawns N single-GPU embedding replicas on consecutive ports, round-robin/least-loaded/random load balancing, health checks, failover retry. |
| `arctic_inference/embedding/replica.py` | (509) One embedding replica (vLLM engine behind gRPC). |
| `arctic_inference/embedding/client.py` | (305) Client SDK. |
| `arctic_inference/embedding/generate_proto.py` | protobuf/grpc codegen. |

### Build / tests / projects
| Path | What |
|---|---|
| `setup.py` / `pyproject.toml` | CMake-driven build of CUDA ext; declares the vLLM plugin entry point; optional extras `[vllm]`, `[embedding]`, `[dynasor]`, `[docs]`, `[test]`. |
| `tests/unit_tests/` | Patching, custom-op parity (fallback vs CUDA), speculator layernorm, spec max-len, nvfp4 cache tests. |
| `tests/benchmarks/` + `benchmark/embedding/` | JSON-mode eval + embedding throughput benchmarks. |
| `projects/` | Runnable offline examples for ulysses / spec_dec / swiftkv / dynasor. |

---

## 3. Notable code (verbatim excerpts)

### 3.1 The `ArcticPatch` framework — `arctic_inference/patching.py:85`
The whole plugin rests on this. `ArcticPatch[Target]` captures the patch target via `__class_getitem__`; `apply_patch()` copies attributes onto the live class/module, rebinds classmethods, and tracks a `_arctic_patches` registry so two patches can't silently clobber the same attribute.

```python
@classmethod
def __class_getitem__(cls, target: Patchable) -> Type:
    if not isinstance(target, Patchable):
        raise TypeError(f"ArcticPatch can only target a class or module, not {type(target)}")
    return type(f"{cls.__name__}[{target.__name__}]", (cls,),
                {'_arctic_patch_target': target})

@classmethod
def apply_patch(cls):
    target = cls._arctic_patch_target
    if "_arctic_patches" not in target.__dict__:
        target._arctic_patches = {}
    for name, attr in cls.__dict__.items():
        if name in ("_arctic_patch_target", "__dict__", "__weakref__",
                    "__module__", "__doc__", "__parameters__",):
            continue
        if name in target._arctic_patches:                 # collision guard
            patch = target._arctic_patches[name]
            raise ValueError(f"{target.__name__}.{name} is already patched by {patch.__name__}")
        target._arctic_patches[name] = cls
        if isinstance(attr, MethodType):                   # rebind classmethods to target
            attr = MethodType(attr.__func__, target)
        setattr(target, name, attr)
```
Convention: patches stash the original as `_orig_<name> = Target.method` so they can delegate. This is a clean, auditable alternative to `unittest.mock.patch` for permanently extending a third-party library you can't fork.

### 3.2 Ulysses attention all-to-all — `arctic_inference/vllm/ulysses.py:386`
Ulysses sequence parallelism: instead of splitting heads across GPUs (TP), it splits the *sequence*, then does an all-to-all so each rank temporarily owns all heads for its sequence slice, runs unmodified attention, and all-to-alls back. Note the KV-replication path for when `num_kv_heads < sp_size`.

```python
def forward(self, query, key, value, **kwargs):
    if self.sp_size == 1 or is_shift_parallel_mode():
        return self._orig_forward(query, key, value, **kwargs)
    q = query.view(-1, self.sp_size, self.num_heads * self.head_size)
    ...
    qkv = torch.cat((q, k, v), dim=-1).transpose(0, 1).reshape(...)
    qkv_ = torch.empty_like(qkv)
    torch.distributed.all_to_all_single(qkv_, qkv, group=self.sp_device_group)   # all-to-all 1/2
    q_, k_, v_ = qkv_.split([...], dim=-1)
    c_ = self._orig_forward(q_, k_, v_, **kwargs)                                # normal attention
    c = torch.empty_like(c_)
    torch.distributed.all_to_all_single(c, c_, group=self.sp_device_group)       # all-to-all 2/2
    output = (c.view(self.sp_size, -1, self.num_heads * self.head_size)
              .transpose(0, 1).reshape(-1, self.num_heads * self.sp_size * self.head_size))
    return output
```

### 3.3 Shift-parallel group swap — `arctic_inference/vllm/model_runner.py:64` & `:581`
"Shift Parallelism" is the paper's headline trick: keep one set of weights but switch the *communication topology* per batch. Small decode batches use full-TP (`_SP_TP`); large prefill batches use sequence-parallel. The swap is a context manager that just repoints vLLM's global `_TP`.

```python
@contextlib.contextmanager
def set_shift_parallel_mode(mode: Optional[bool]):
    if mode is None:
        yield; return
    global SP_TP_MODE
    if not is_shift_parallel_mode():
        parallel_state._ORIG_TP = parallel_state._TP
    old_tp_group = parallel_state.get_tp_group()
    SP_TP_MODE = mode
    parallel_state._TP = (parallel_state._SP_TP if mode else parallel_state._ORIG_TP)
    try:
        yield
    finally:
        SP_TP_MODE = old_mode
        parallel_state._TP = old_tp_group
```
```python
# forward-batch routing:
use_shift_model = (getattr(self, "use_ulysses", False)
                   and getattr(self, "shift_model", None) is not None
                   and num_scheduled_tokens <= self.shift_parallel_threshold)
```

### 3.4 Suffix-decoding dual-tree speculation — `arctic_inference/suffix_decoding/cache.py:290`
A *model-free* speculator: it matches the recent context against two suffix trees — a per-request local tree (the prompt) and a global tree (all previous responses) — and drafts the highest-scoring continuation. Zero-copy int32 ndarray fast path, and clever int32 seq-id recycling with wrap-around collision handling (`_generate_seq_id`).

```python
draft1 = spec_func(self._local_trees[req_id], context, max_spec_tokens,
                   max_spec_factor, max_spec_offset, min_token_prob, use_tree_spec)
draft2 = spec_func(self._global_tree, context, max_spec_tokens,
                   max_spec_factor, max_spec_offset, min_token_prob, use_tree_spec)
draft = draft1 if draft1.score >= draft2.score else draft2
return SuffixDecodingDraft.from_native(draft)
```
The speculation budget is adaptive: `max_spec_factor * match_length + max_spec_offset` — longer verified matches earn longer drafts.

### 3.5 Graceful custom-op loading with fallback — `arctic_inference/py_custom_ops.py:9`
Every custom kernel has a pure-torch twin; the `.so` is discovered at runtime (handles the `.cpython-310-*.so` ABI suffix) and a failed load is a warning, not a crash. `USE_CUSTOM_OP` then gates `forward_opt` vs `forward_fallback` (see `MLPSpeculatorLayerNorm`).

```python
for file in os.listdir(package_path):
    if file.startswith('custom_ops') and file.endswith('.so'):
        library_path = os.path.join(package_path, file); break
else:
    logger.info("Could not find compiled custom_ops library in package."); return False
try:
    torch.ops.load_library(library_path); return True
except RuntimeError as e:
    logger.info(f"...Falling back to original implementation."); return False
```

### 3.6 Reasoning early-exit by answer entropy — `arctic_inference/dynasor/entropy.py:208`
Dynasor periodically "probes" a reasoning model for its current answer; if the last *N* probe answers agree, contain no hedging words (`wait/hmm/but/...`), and are all non-empty & certain, generation stops early — saving tokens on easy problems.

```python
def should_early_exit(answers, probe_response_text, uncertain_words,
                      continue_certain_bar, is_certains) -> bool:
    if len(answers) < continue_certain_bar:
        return False
    if any(word in probe_response_text.lower() for word in uncertain_words):
        return False
    answer_candidates = answers[-continue_certain_bar:]
    if equal_group(answer_candidates) \
       and count_not_empty(answer_candidates) == continue_certain_bar \
       and sum(is_certains[-continue_certain_bar:]) == continue_certain_bar:
        return True
    return True
```

---

## 4. Extractable value

- **`ArcticPatch` monkey-patch framework** (`patching.py`, ~140 self-contained LOC, no deps): the single most reusable piece. A disciplined way to extend/override a third-party library you cannot fork, with **collision detection** (two patches touching the same attribute raise), automatic classmethod rebinding, and an audit log of every replaced symbol. Directly transplantable to any project that needs to patch a vendored dependency. Conceptually close to a "removed/added symbol guard" — pairs well with build-time verification that patches still apply.
- **Entry-point plugin pattern**: register one function under the host framework's plugin group, gate on env + version + platform, and apply everything lazily *after* process fork (the `WorkerBase.__init__` deferral to dodge CUDA-fork breakage is a real, non-obvious gotcha worth copying for any CUDA-in-multiprocessing code).
- **Context-manager topology swap** (`set_shift_parallel_mode`): the pattern of temporarily repointing a global distributed group and restoring it in `finally` is a clean template for any "same weights, different collective layout per batch" optimization.
- **Dual-tree suffix speculation** (`cache.py`): a complete, model-free draft-generation algorithm with local+global caches, FIFO eviction, adaptive draft budget, and a robust int32 seq-id recycler with wrap-around collision handling. Reusable for prompt-caching / autocomplete / any repetitive-generation workload independent of vLLM.
- **Graceful native-op loading** (`py_custom_ops.py` + paired `forward_opt`/`forward_fallback`): ship a fast CUDA path and a pure-torch fallback behind one boolean, discovered at import with ABI-suffix-tolerant `.so` search. Good template for any optional-accelerator library.
- **DeepSpeed-style `OpBuilder`** (`op_builder/builder.py`): battle-tested JIT CUDA compilation with compute-capability autodetection and torch/system CUDA mismatch tolerance — liftable wholesale for other torch C++/CUDA extension projects.
- **Answer-entropy early-exit loop** (`dynasor/entropy.py`): a provider-agnostic heuristic for stopping reasoning-model generation once repeated self-consistent answers appear — applicable to any test-time-scaling / self-consistency setup.
- **Single-GPU multi-replica gRPC server** (`embedding/replica_manager.py`): reusable pattern for packing several inference replicas onto one device behind a load-balancing, health-checking, failover gRPC front.

---

## 5. Build / run

**Install (prebuilt path):**
```bash
pip install arctic-inference[vllm]     # pulls vllm==0.11.0
```
Enable at runtime by setting `ARCTIC_INFERENCE_ENABLED=1`; vLLM auto-loads the plugin via the `vllm.general_plugins` entry point. Serving example:
```bash
ARCTIC_INFERENCE_ENABLED=1 vllm serve Snowflake/Llama-3.1-SwiftKV-8B-Instruct \
    --quantization fp8 --tensor-parallel-size 1 --ulysses-sequence-parallel-size 2 \
    --enable-shift-parallel \
    --speculative-config '{"method":"arctic","model":"Snowflake/Arctic-LSTM-Speculator-Llama-3.1-8B-Instruct","num_speculative_tokens":3,"enable_suffix_decoding":true,"disable_by_batch_size":64}'
```
Offline: `vllm.plugins.load_general_plugins()` then construct `LLM(...)` with `ulysses_sequence_parallel_size`, `enable_shift_parallel`, `speculative_config` (see README).

**Build from source (compiles CUDA ext):** requires CUDA toolchain + `torch==2.8.0`. `pyproject.toml` build-system pulls `cmake`, `ninja`, `nanobind==2.9.2`, `grpcio-tools`, `protobuf==5.29.5`; `setup.py` drives a CMake build of `csrc/custom_ops` into `custom_ops*.so`. If the `.so` is missing, everything still runs on pure-torch fallbacks. Embedding server needs `[embedding]` extra (gRPC); Dynasor needs `[dynasor]` (sympy/latex2sympy/word2number).

**Test:** `pip install arctic-inference[test]`; `pytest tests/unit_tests` (patching, custom-op vs fallback parity, speculator layernorm, nvfp4 cache).

**Compatibility note:** the plugin hard-checks `vllm.__version__` against a pinned compatible version (override via `ARCTIC_INFERENCE_SKIP_VERSION_CHECK`) — it patches deep vLLM internals (`parallel_state`, `GPUModelRunner`, `MultiprocExecutor`) so it is tightly coupled to one vLLM release.
