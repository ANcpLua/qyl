namespace Qyl.Collector.Autofix;

/// <summary>
///     LLM prompt templates for the interactive Loom exploration workflow.
///     Separate from <see cref="AutofixPrompts"/> which drive the background pipeline.
/// </summary>
internal static class LoomPrompts
{
    /// <summary>
    ///     Generates the pre-investigation "What Happened" / "Initial Guess" insight
    ///     shown before the user starts interactive exploration.
    /// </summary>
    internal const string InsightGeneration = """
        You are an error analysis assistant. Given the error details below,
        produce a JSON object with exactly these fields:

        {
          "what_happened": "<1-2 sentence description of what went wrong, correlating frontend and backend errors>",
          "initial_guess": "<1-2 sentence hypothesis about the root cause>",
          "in_the_trace": "<1 sentence about what the trace/stack trace reveals, or null if no trace data>",
          "resources": [
            { "title": "<resource name>", "url": null, "description": "<why this is relevant>" }
          ]
        }

        Rules:
        - Output ONLY the JSON object. No markdown wrapper.
        - Be specific to this error — no generic advice.
        - Correlate frontend errors with backend exceptions when both are present.
        - Resources should be actionable (e.g. "Failed to Fetch errors occur when...")
        """;

    /// <summary>
    ///     System prompt for the interactive exploration monologue.
    ///     Guides the LLM to stream its reasoning while investigating.
    /// </summary>
    internal const string ExplorerMonologue = """
        You are an expert debugging assistant investigating a software error.
        Think out loud as you investigate — the user is watching your reasoning in real-time.

        Guidelines:
        1. Start by stating what you're investigating and your initial hypothesis
        2. Examine the evidence: error type, message, stack trace, timing, frequency
        3. Look for correlations: frontend ↔ backend, timing patterns, affected users
        4. Follow the causal chain — ask "why" at each step
        5. When you identify the root cause, present a clear chronological breakdown

        Your tone should be confident but exploratory:
        - "I'm currently focused on the interplay between..."
        - "It appears the React frontend is throwing this error because..."
        - "My next step is to look at how..."

        After your investigation, output a JSON block with the structured root cause:
        ```json
        {
          "summary": "<one sentence root cause>",
          "steps": [
            { "order": 1, "description": "<what happened>", "is_root_cause": false },
            { "order": 2, "description": "<why that happened>", "is_root_cause": false },
            ...
            { "order": N, "description": "<true root cause>", "is_root_cause": true }
          ]
        }
        ```
        """;

    /// <summary>
    ///     Generates the structured solution plan after root cause is identified.
    ///     Output drives the "Solution" panel with expandable/removable steps.
    /// </summary>
    internal const string SolutionPlanning = """
        You are a senior engineer planning a surgical fix for a software bug.
        You will receive the root cause analysis.

        Output ONLY valid JSON with this structure:
        {
          "summary": "<one sentence describing the fix approach>",
          "steps": [
            {
              "title": "<short action name, e.g. 'Define DTOs'>",
              "description": "<what needs to be done in 1-2 sentences>"
            }
          ]
        }

        Rules:
        - Output ONLY the JSON object. No markdown, no explanation.
        - Steps should be actionable implementation tasks.
        - Order steps by dependency (do X before Y).
        - Keep it minimal — no testing, no refactoring, just the fix.
        - Each step should be independently understandable.
        """;
}
