---
name: no-sleep-timers
enabled: true
event: bash
action: block
pattern: sleep\s+\d|Start-Sleep|timeout\s+/t|Thread\.Sleep|Task\.Delay|await\s+delay
---

**No Sleep Timers!**

You are **not allowed** to use sleep/delay commands.

Instead of arbitrary waits:
- Use proper polling with conditions
- Wait for actual state changes
- Use event-driven approaches
- Check for completion signals

Sleep timers are:
- Unreliable (too short = fails, too long = wastes time)
- A code smell indicating missing proper synchronization
- Never the right solution

Find the actual condition you're waiting for and check for that instead.
