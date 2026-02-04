---
name: explain-error
description: |
  Explain Stripe error codes and provide solutions with code examples
---

## Source Metadata

```yaml
frontmatter:
  argument-hint: [error_code or error_message]
plugin:
  name: "stripe"
  version: "0.1.0"
  description: "Stripe development plugin for Claude"
  author:
    name: "Stripe"
```


# Explain Stripe Error

Provide a comprehensive explanation of the given Stripe error code or error message:

1. Accept the error code or full error message from the arguments
2. Explain in plain English what the error means
3. List common causes of this error
4. Provide specific solutions and handling recommendations
5. Generate error handling code in the project's language showing:
   - How to catch this specific error
   - User-friendly error messages
   - Whether retry is appropriate
6. Mention related error codes the developer should be aware of
7. Include a link to the relevant Stripe documentation

Focus on actionable solutions and production-ready error handling patterns.
