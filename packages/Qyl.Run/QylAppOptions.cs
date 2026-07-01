
using System.ComponentModel.DataAnnotations;

namespace Qyl.Run;

public sealed class QylAppOptions
{
    public const string SectionName = "Qyl:Run";

    [Range(0, 65535, ErrorMessage = $"{SectionName}:{nameof(RunnerPort)} must be a valid TCP port (0 = auto-allocate)")]
    public int RunnerPort { get; set; } = QylConstants.Ports.RunnerApi;

    [Required(ErrorMessage = $"{SectionName}:{nameof(RunnerHost)} is required")]
    public string RunnerHost { get; set; } = QylConstants.Network.Loopback;

    [Range(1, 600, ErrorMessage = $"{SectionName}:{nameof(StartupTimeoutSeconds)} must be 1..600")]
    public int StartupTimeoutSeconds { get; set; } = QylConstants.Orchestrator.StartupTimeoutSeconds;

    public bool CaptureChildOutput { get; set; } = true;
}
