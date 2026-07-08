# AutoMetrics — Extraction Dossier

**One-line summary:** A Python research library (ICLR 2026, SALT-NLP/Stanford) that automatically *induces* a single interpretable evaluation metric approximating human judgment from <100 labeled examples, by generating LLM-judge/code metrics, retrieving complementary metrics from a curated bank of ~48, and composing them with PLS regression.

**Stack / language:** Python ≥3.9. Core deps: `dspy` (LLM programs / signatures / MIPROv2), `scikit-learn` (PLS + regression zoo), `numpy`/`scipy`/`pandas`, `diskcache` (per-metric caching), `litellm` (token counting/model metadata), `torch`/`transformers` (fine-tune + reward-model metrics), `deno` (sandboxed code-metric execution). Heavy metric deps (bert_score, pyserini/Java, pylate/ColBERT, rouge, mauve, …) are **lazily imported** so the generated-only path stays lightweight. ~55.8k LOC total (majority in the 48-metric bank + docs).

---

## 1. Architecture overview

AutoMetrics is a 5-stage pipeline orchestrated by `Autometrics.run()` (`autometrics/autometrics.py`, ~2135 LOC):

```
Dataset (id,input,output,score,[references]) + task_description
   │
   ├─0. Priors (optional): eval user-pinned metrics up front
   │
   ├─1. GENERATE  — DSPy proposers invent task-specific candidate metrics:
   │      LLM-judge (axes of variation), rubric (DSPy/Prometheus), G-Eval,
   │      code-gen (sandboxed), MIPROv2-optimized judge, example-based judge,
   │      fine-tuned ModernBERT regressor. (10+5+1+1 by default)
   │
   ├─2. LOAD BANK — 48 curated metrics (reference-based + reference-free),
   │      lazily imported; auto-drops ref-based ones if no reference cols.
   │      Small-dataset (≤100 rows) → generated-only mode, bank skipped.
   │
   ├─3. RETRIEVE  — PipelinedRec: (BM25|ColBERT) → LLMRec reranker,
   │      top_ks monotonically decreasing (e.g. 60 → 30). Token-budgeted
   │      batching with binary search over batch size.
   │
   ├─4. EVALUATE  — run top-k metrics over dataset (parallel ThreadPool,
   │      per-metric diskcache, graceful failure budget).
   │
   ├─5. REGRESS   — PLS (default) fits StandardScaler→model on metric columns,
   │      selects top-N by |coefficient|, drops negative-coef generated metrics.
   │
   └─6. REPORT    — emits (a) importable Python class (static, sklearn-free),
          (b) per-metric Metric Cards, (c) HTML report: coefficients,
          correlation (Spearman/Kendall + p-values), robustness, runtime,
          per-example feedback.
```

Every metric is a `Metric` subclass with a uniform `calculate/predict` interface, transparent MD5-keyed diskcache, and optional feedback (`MetricResult(score, feedback)`). The **aggregator** is itself a `Metric`, so the final composed evaluator is a drop-in metric that can be serialized to standalone Python.

Key design axes:
- **Contract uniformity:** generators, recommenders, aggregators, and metrics all share small ABCs (`Generator`, `MetricRecommender`, `Aggregator`, `Metric`), making each stage swappable via constructor args.
- **Lazy heavy deps:** a `_DEFAULT_METRIC_BANK` sentinel resolves the bank only inside `run()`, and `MetricBank` wraps every import in try/except so partial installs still work.
- **Split datasets:** `run()` accepts separate `generation_dataset` / `regression_dataset` / `eval_dataset` — propose criteria from a large pool, fit regression on the small labeled set.

---

## 2. File / module map (significant sources)

### Orchestration
| Path | What |
|---|---|
| `autometrics/autometrics.py` | Main `Autometrics` pipeline: generate→retrieve→evaluate→regress→report; GPU-aware retriever defaults, parallel eval, priors, small-dataset cutoff, negative-coef dropping. |
| `autometrics/dataset/Dataset.py` | Pandas-backed dataset with typed columns (input/output/target/reference/metric/ignore); train/val/test + k-fold splits, permanent-split persistence + consistency validation, subset/copy. |
| `autometrics/dataset/PairwiseDataset.py`, `ChosenRejectedDataset.py` | Pairwise / preference (chosen-vs-rejected) dataset variants. |

### Metrics core
| Path | What |
|---|---|
| `autometrics/metrics/Metric.py` | `Metric` ABC + `MetricResult` dataclass. Transparent diskcache with deterministic MD5 keys from init params + args (excluding label/cache-config params), batched cache, feedback variants. |
| `autometrics/metrics/MultiMetric.py` | Metric producing several sub-scores in one pass (e.g. HuggingFace multi-metrics). |
| `autometrics/metrics/PairwiseMetric.py`, `PairwiseMultiMetric.py` | Pairwise-comparison metric bases. |
| `autometrics/metrics/MetricBank.py` | Registry of ~48 metrics as *classes* (never instantiated at import); `reference_based_metric_classes`, `reference_free_metric_classes`, `all_metric_classes`; factory `build_metrics(...)`. |
| `autometrics/metrics/reference_based/*.py` (29) | BLEU, CHRF, TER, GLEU, SARI, BERTScore, ROUGE, MOVERScore, BARTScore, UniEval*, CIDEr, METEOR, ParaScore, YiSi, MAUVE, NIST, IBLEU, BLEURT, LENS, CharCut… |
| `autometrics/metrics/reference_free/*.py` (23) | Toxicity, Sentiment, FKGL/readability, Perplexity, DistinctNGram, SelfBLEU, FactCC, SummaQA, UniEvalFact, FastText* (NSFW/toxicity/edu-value), and reward models (LDL/GRM/PRM/INFORM). |
| `autometrics/metrics/generated/*.py` | Runtime metric shells the generators fill: `GeneratedLLMJudgeMetric`, `GeneratedCodeMetric`, `GeneratedGEvalMetric`, `GeneratedPrometheus`, `GeneratedExampleRubric`, `GeneratedOptimizedJudge`, `GeneratedFinetunedMetric`, ref-free/ref-based bases. Each can emit its own standalone Python via `_generate_python_code`. |

### Generators (Stage 1)
| Path | What |
|---|---|
| `autometrics/generator/Generator.py` | `Generator` ABC: `generate(dataset, target_measure, n_metrics)` → list of `Metric`; holds generator_llm + executor class/kwargs. |
| `autometrics/generator/LLMJudgeProposer.py` | Proposes "axes of variation" from good/bad examples via DSPy, wraps each axis in a ref-free/ref-based LLM-judge metric. |
| `autometrics/generator/OptimizedJudgeProposer.py` | Same axes but runs **MIPROv2** to optimize the judge prompt per axis; eval fns `exact_match_rounded`/`inverse_distance`. |
| `autometrics/generator/LLMJudgeExampleProposer.py` | Few-shot / example-grounded judge. |
| `autometrics/generator/RubricGenerator.py`, `GEvalJudgeProposer.py` | Rubric-based (DSPy + Prometheus) and G-Eval-style judges. |
| `autometrics/generator/CodeGenerator.py` | LLM writes a `compute_score(input,output,references)->float`, run in a Deno sandbox; **self-healing** `FixCodeSignature` repairs broken code from the traceback + samples. |
| `autometrics/generator/FinetuneGenerator.py` | Fine-tunes `answerdotai/ModernBERT-large` (PEFT/LoRA) as a regression metric on the user's labels. |
| `autometrics/generator/utils.py` | `get_good_bad_examples`, `generate_axes_of_variation`, token-aware example truncation, DSPy prompt-token estimation. |

### Recommenders (Stage 3)
| Path | What |
|---|---|
| `autometrics/recommend/MetricRecommender.py` | ABC + `metric_name_to_class` resolver. |
| `autometrics/recommend/PipelinedRec.py` | Chains recommenders with monotonically-decreasing top_ks (e.g. BM25→LLMRec). |
| `autometrics/recommend/LLMRec.py` | LLM reranker with **systematic token budgeting**: binary-search max batch size, recursive context-error splitting, iterative quota-filling. |
| `autometrics/recommend/BM25.py`, `ColBERT.py`, `Faiss.py` | Sparse (pyserini/Java), late-interaction (pylate), and dense retrieval backends. |

### Aggregators (Stage 5)
| Path | What |
|---|---|
| `autometrics/aggregator/Aggregator.py` | `Aggregator(Metric)` base: input metrics as dependencies, `ensure_dependencies`. |
| `autometrics/aggregator/regression/Regression.py` | Core regression aggregator: StandardScaler + inf-clipping, coefficient/importance extraction, feature-vector assembly (scalar + multi), **`export_python`** to a standalone sklearn-free module. |
| `autometrics/aggregator/regression/{PLS,HotellingPLS,Ridge,Lasso,ElasticNet,Linear,RandomForest,GradientBoosting,PLS…}.py` | Regression strategy zoo; PLS is default. `BudgetRegression` adds cost-aware selection. |
| `autometrics/aggregator/generated/GeneratedRegressionMetric.py` | `GeneratedStaticRegressionAggregator`: runtime-only `y=((X_clip-mean)/scale)@coef+intercept`, no sklearn; the exported class' base. |

### Reporting / utils / experiments
| Path | What |
|---|---|
| `autometrics/util/report_card.py` | HTML report card: coefficient table, correlation (Spearman/Kendall + p-value), robustness, runtime, examples table, DSPy `MetricSummary`, `render_html`. |
| `autometrics/util/custom_python_interpreter.py` + `custom_runner.js` | Deno-based sandbox that cleanly separates stdout/stderr from the return value (fixes DSPy's package-load noise) for code metrics. |
| `autometrics/util/{analysis,normalize,format,metric_eval_utils}.py` | Top-metric-by-validation selection, score normalization, dataset row formatters, eval helpers. |
| `autometrics/evaluate/{correlation,accuracy}.py` | `calculate_correlation` + `calculate_correlation_with_p_val` (grouped Spearman/Kendall/Pearson). |
| `autometrics/experiments/{experiment,robustness,correlation,timing,results,recommendation}.py` | Reusable experiment harness (paper ablations): robustness perturbations, timing, tabular results. |

---

## 3. Notable code (verbatim excerpts)

### 3a. Transparent, deterministic per-metric cache — `autometrics/metrics/Metric.py:114`
Every metric gets content-addressed caching keyed on init params + call args, with label/cache-config params explicitly excluded so they don't bust the key. This is what makes re-runs of the (expensive, LLM-backed) pipeline cheap.
```python
def _make_cache_key(self, method_name, *args, **kwargs):
    components = [method_name]
    for k, v in sorted(self._init_params.items()):
        if k not in self._excluded_params:            # name/description/cache-cfg excluded
            components.append(f"init_{k}={self._make_hashable(v)}")
    for arg in args:
        components.append(self._make_hashable(arg))
    for k, v in sorted(kwargs.items()):
        components.append(f"{k}={self._make_hashable(v)}")
    key_str = "_".join(str(c) for c in components)
    return hashlib.md5(key_str.encode()).hexdigest()
```
`_make_hashable` canonicalizes lists (sorted) and dicts recursively; exceptions are re-raised *without* being cached (`calculate`, line 156) so transient LLM failures don't poison the cache.

### 3b. Token-budgeted LLM reranking with binary search — `autometrics/recommend/LLMRec.py:279`
The reranker never blindly stuffs the context. It measures avg per-metric doc tokens, computes an available budget (reserving DSPy overhead / output / safety margin, scaled down for small contexts), then binary-searches the largest batch that fits:
```python
left, right = 1, len(metric_classes)
max_metrics_per_batch = 1
while left <= right:
    mid = (left + right) // 2
    estimated_tokens = self._estimate_prompt_tokens(
        task_description, target_measurement, mid, avg_metric_doc_tokens)
    if estimated_tokens <= available_tokens:
        max_metrics_per_batch = mid
        left = mid + 1
    else:
        right = mid - 1
```
Paired with `_process_split_batch` (recursive halving on any context-window error, with `_extract_max_allowed_from_error` learning the proxy's real limit from the error text) and `recommend()`'s iterative quota-filling loop (keeps requesting until ≥90% of `k` is met, max 5 iterations). A robust pattern for "ask an LLM to rank N items that don't fit in one prompt."

### 3c. Code-metric self-healing — `autometrics/generator/CodeGenerator.py:20`
Generated `compute_score` code that throws is repaired by feeding the LLM the broken code, the exact error, and the sample input/output that triggered it:
```python
class FixCodeSignature(dspy.Signature):
    """You are an expert Python programmer tasked with fixing broken code generation...
    - Return working Python code that executes without errors
    - Preserve the original measurement intention
    - Handle edge cases (empty strings, None values, type mismatches) ..."""
    broken_code: str      = dspy.InputField(...)
    error_message: str    = dspy.InputField(...)
    sample_input: str     = dspy.InputField(...)
    fix_explanation: str  = dspy.OutputField(desc="...Do not include any code...")
    fixed_code: str       = dspy.OutputField(desc="...Surround with ```python and ```.")
```
Execution happens in a Deno sandbox (`custom_python_interpreter.py`) with `--allow-read/net/write` only, isolating untrusted generated code from the host process.

### 3d. Static, sklearn-free export of the fitted evaluator — `autometrics/aggregator/regression/Regression.py:426`
The fitted PLS aggregator serializes itself to a standalone module: it inlines each component generated metric's source (with constant-name uniquification to avoid collisions) and bakes in coefficients + scaler stats, so the exported evaluator has **no sklearn / no AutoMetrics-pipeline runtime dependency**:
```python
class {class_def_name}(GeneratedStaticRegressionAggregator):
    def __init__(self):
        super().__init__(
            name={repr(salted_name)}, input_metrics=INPUT_METRICS,
            feature_names={repr(list(feature_names))},
            coefficients={repr([float(x) for x in coef_arr])},
            intercept={float(intercept_val)},
            scaler_mean={repr([float(x) for x in mean_list)])},
            scaler_scale={repr([float(x) for x in scale_list)])})
```
At runtime the static base (`GeneratedRegressionMetric.py:10`) computes `y = ((X_clip - mean)/scale) @ coef + intercept` directly in numpy, mirroring training-time inf-clipping/NaN handling.

### 3e. Coefficient-ordered feedback aggregation — `autometrics/aggregator/regression/Regression.py:137`
The composed metric's *feedback* is assembled from component metrics in descending |coefficient| order (most-influential rationale first), de-duplicated per row — so the final evaluator explains itself:
```python
ordered_feats = sorted(name_to_coef.items(), key=lambda p: abs(p[1]), reverse=True)
def _combine_feedback(row):
    seen, out_lines = set(), []
    for c in feedback_cols:                     # cols ordered by |coef|
        txt = row.get(c)
        if isinstance(txt, str) and txt.strip() and txt.strip() not in seen:
            seen.add(txt.strip()); out_lines.append(txt.strip())
    return "\n".join(out_lines)
```

### 3f. Lazy, failure-tolerant metric registry — `autometrics/metrics/MetricBank.py:22`
Each of the 48 metrics is imported defensively so a missing optional dep (Java, pylate, rouge…) silently drops just that metric instead of breaking the whole bank:
```python
def _try_import(module_path, *names):
    try:
        module = __import__(module_path, fromlist=list(names))
    except Exception as exc:
        _warnings.warn(f"[MetricBank] Skipping {module_path} ({', '.join(names)}): {exc}", RuntimeWarning)
        return {}
    return {n: getattr(module, n) for n in names if hasattr(module, n)}
```

---

## 4. Extractable value (reusable patterns)

1. **Content-addressed cache mixin for expensive callables** (`Metric._make_cache_key`/`calculate`): MD5 over canonicalized init-params+args, with an explicit *exclusion set* for params that don't affect output, diskcache-backed, exceptions never cached. Directly liftable to any "cache LLM/network calls with a stable key" need — including qyl-style telemetry where you want to memoize span-derived computations without caching transient errors.
2. **Token-budgeted "rank items that don't fit in one prompt" algorithm** (`LLMRec`): binary-search max batch, recursive halving on context errors, learn the real limit from the error message, iterative quota-fill to a target count. This is a general-purpose LLM-fan-out primitive.
3. **LLM code self-healing loop** (`FixCodeSignature` + Deno sandbox): generate → execute in a permissioned sandbox → on failure feed (code, error, triggering sample) back for repair. A clean template for any "LLM writes code you must safely run" feature.
4. **Sklearn-free model export via source-inlining code generation** (`Regression.export_python` + `GeneratedStaticRegressionAggregator`): freeze a trained scaler+linear model into a dependency-light standalone module, inlining component source with constant-name de-collision. Great pattern for shipping a fitted model without the training stack.
5. **Lazy/defensive plugin registry** (`MetricBank._try_import` + `_DEFAULT_METRIC_BANK` sentinel resolved at call time): let heavy optional backends fail independently and keep the light path free of their imports. Applicable to any plugin/extension system with heterogeneous deps.
6. **Uniform swappable-stage ABCs** (`Generator`/`MetricRecommender`/`Aggregator`/`Metric` all small ABCs; aggregator *is* a metric): every pipeline stage is a constructor arg. A tidy blueprint for composable ML pipelines.
7. **Sandboxed interpreter that cleanly separates return value from stdout/stderr** (`custom_python_interpreter.py` + `custom_runner.js`): reusable wherever you exec untrusted Python and need the actual result, not log noise.
8. **Grouped correlation with p-values** (`evaluate/correlation.py`): per-group Spearman/Kendall/Pearson averaging with significance — a compact metric-vs-human agreement evaluator.

---

## 5. Build / run

```bash
pip install autometrics-ai            # base install: Python 3.9+ only
export OPENAI_API_KEY="sk-..."
python examples/tutorial.py           # 8-row demo, generated-only, no Java/GPU/bank
```
- **Optional extras** (metric-specific clusters) declared in `pyproject.toml`: `autometrics-ai[bert-score]`, `[rouge]`, `[mauve]`, `[reward-models]`, `[gpu]`, `[anthropic]`, etc. — pulled only when that metric is used.
- **Full pipeline** (`examples/autometrics_simple_example.py`) additionally needs **Java 21** (pyserini/BM25) and bank extras; code metrics need **Deno** installed.
- **Tests:** `pytest` (config in `pytest.ini`); suite under `autometrics/test/` covers cache, splits, code-gen + validation + integration, LLM-judge/G-Eval, pairwise metrics, GPU allocation, report-card p-values, init-param caching.
- **Programmatic use:** build a `Dataset` (id/input/output/score cols + `task_description`), then `Autometrics().run(dataset, target_measure="score", generator_llm=dspy.LM(...), judge_llm=dspy.LM(...))`; result dict exposes `regression_metric` (importable `Metric`), `top_metrics`, `report_card`, `all_generated_metrics`.
- **Env:** `AUTOMETRICS_CACHE_DIR` (default `./autometrics_cache`), `OPENAI_API_KEY`/provider keys via litellm/dspy.
- Publishing via `.github/workflows/python-publish.yml`; version single-sourced in `VERSION`/`pyproject.toml` (0.1.0).
