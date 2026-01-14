// =============================================================================
// QYL v2.0 - CLI Semantic Conventions (OTel v1.39)
// =============================================================================
// Command-line interface execution telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.AI.Cli;

// =============================================================================
// CLI Span Attributes
// =============================================================================

@doc("Command-line interface execution attributes")
model CliAttributes {
  @doc("The command being executed (without arguments)")
  @encodedName("application/json", "cli.command")
  command?: string;

  @doc("Full command line (command + arguments)")
  @encodedName("application/json", "cli.command_line")
  commandLine?: string;

  @doc("Command arguments as array")
  @encodedName("application/json", "cli.command_args")
  commandArgs?: string[];

  @doc("Exit code of the command")
  @encodedName("application/json", "cli.exit_code")
  exitCode?: int32;

  @doc("Whether the command succeeded (exit code 0)")
  @encodedName("application/json", "cli.success")
  success?: boolean;

  @doc("Working directory when command was executed")
  @encodedName("application/json", "cli.working_directory")
  workingDirectory?: string;

  @doc("User who executed the command")
  @encodedName("application/json", "cli.user")
  user?: string;

  @doc("Shell used to execute the command")
  @encodedName("application/json", "cli.shell")
  shell?: CliShell;

  @doc("Whether running in interactive mode")
  @encodedName("application/json", "cli.interactive")
  interactive?: boolean;

  @doc("Whether running with elevated privileges")
  @encodedName("application/json", "cli.elevated")
  elevated?: boolean;
}

// =============================================================================
// CLI Enumerations
// =============================================================================

@doc("Shell types")
enum CliShell {
  @doc("Bash shell")
  bash: "bash",

  @doc("Zsh shell")
  zsh: "zsh",

  @doc("Fish shell")
  fish: "fish",

  @doc("PowerShell")
  powershell: "powershell",

  @doc("Windows Command Prompt")
  cmd: "cmd",

  @doc("POSIX sh")
  sh: "sh",

  @doc("Tcsh shell")
  tcsh: "tcsh",

  @doc("Ksh shell")
  ksh: "ksh",

  @doc("Unknown shell")
  `unknown`: "unknown",
}

@doc("CLI invocation types")
enum CliInvocationType {
  @doc("Direct user invocation")
  direct: "direct",

  @doc("Invoked from a script")
  script: "script",

  @doc("Invoked from CI/CD pipeline")
  cicd: "cicd",

  @doc("Invoked by another process")
  subprocess: "subprocess",

  @doc("Scheduled/cron invocation")
  scheduled: "scheduled",
}

// =============================================================================
// CLI Execution Model
// =============================================================================

@doc("Complete CLI execution record")
model CliExecution {
  @doc("Command name")
  command: string;

  @doc("Command arguments")
  arguments: string[];

  @doc("Environment variables (filtered for safety)")
  environment?: CliEnvironment[];

  @doc("Start time")
  @encodedName("application/json", "start_time")
  startTime: utcDateTime;

  @doc("End time")
  @encodedName("application/json", "end_time")
  endTime?: utcDateTime;

  @doc("Duration in milliseconds")
  @encodedName("application/json", "duration_ms")
  durationMs?: DurationMs;

  @doc("Exit code")
  @encodedName("application/json", "exit_code")
  exitCode?: int32;

  @doc("Standard output (truncated)")
  stdout?: string;

  @doc("Standard error (truncated)")
  stderr?: string;

  @doc("Invocation type")
  @encodedName("application/json", "invocation_type")
  invocationType?: CliInvocationType;
}

@doc("CLI environment variable (filtered)")
model CliEnvironment {
  @doc("Variable name")
  name: string;

  @doc("Whether the value is set (not the actual value for security)")
  @encodedName("application/json", "is_set")
  isSet: boolean;

  @doc("Value (only for safe variables)")
  value?: string;
}
