// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-03-06T15:59:59.2394690+00:00
//     Enumeration types for Qyl.OTel.Resource
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.OTel.Resource;

/// <summary>Cloud provider types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<CloudProvider>))]
public enum CloudProvider
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("alibaba_cloud")]
    AlibabaCloud = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("aws")]
    Aws = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("azure")]
    Azure = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("gcp")]
    Gcp = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("heroku")]
    Heroku = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ibm_cloud")]
    IbmCloud = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("tencent_cloud")]
    TencentCloud = 6,
}

/// <summary>Host architecture types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<HostArch>))]
public enum HostArch
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("amd64")]
    Amd64 = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("arm32")]
    Arm32 = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("arm64")]
    Arm64 = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ia64")]
    Ia64 = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ppc32")]
    Ppc32 = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ppc64")]
    Ppc64 = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("s390x")]
    S390x = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("x86")]
    X86 = 7,
}

/// <summary>Operating system types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<OsType>))]
public enum OsType
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("windows")]
    Windows = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("linux")]
    Linux = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("darwin")]
    Darwin = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("freebsd")]
    Freebsd = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("netbsd")]
    Netbsd = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("openbsd")]
    Openbsd = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("dragonflybsd")]
    Dragonflybsd = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("hpux")]
    Hpux = 7,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("aix")]
    Aix = 8,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("solaris")]
    Solaris = 9,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("z_os")]
    ZOs = 10,
}

