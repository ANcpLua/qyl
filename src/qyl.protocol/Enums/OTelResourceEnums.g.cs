// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9038800+00:00
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
    [System.Runtime.Serialization.EnumMember(Value = "alibaba_cloud")]
    AlibabaCloud = 0,
    [System.Runtime.Serialization.EnumMember(Value = "aws")]
    Aws = 1,
    [System.Runtime.Serialization.EnumMember(Value = "azure")]
    Azure = 2,
    [System.Runtime.Serialization.EnumMember(Value = "gcp")]
    Gcp = 3,
    [System.Runtime.Serialization.EnumMember(Value = "heroku")]
    Heroku = 4,
    [System.Runtime.Serialization.EnumMember(Value = "ibm_cloud")]
    IbmCloud = 5,
    [System.Runtime.Serialization.EnumMember(Value = "tencent_cloud")]
    TencentCloud = 6,
}

/// <summary>Host architecture types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<HostArch>))]
public enum HostArch
{
    [System.Runtime.Serialization.EnumMember(Value = "amd64")]
    Amd64 = 0,
    [System.Runtime.Serialization.EnumMember(Value = "arm32")]
    Arm32 = 1,
    [System.Runtime.Serialization.EnumMember(Value = "arm64")]
    Arm64 = 2,
    [System.Runtime.Serialization.EnumMember(Value = "ia64")]
    Ia64 = 3,
    [System.Runtime.Serialization.EnumMember(Value = "ppc32")]
    Ppc32 = 4,
    [System.Runtime.Serialization.EnumMember(Value = "ppc64")]
    Ppc64 = 5,
    [System.Runtime.Serialization.EnumMember(Value = "s390x")]
    S390x = 6,
    [System.Runtime.Serialization.EnumMember(Value = "x86")]
    X86 = 7,
}

/// <summary>Operating system types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<OsType>))]
public enum OsType
{
    [System.Runtime.Serialization.EnumMember(Value = "windows")]
    Windows = 0,
    [System.Runtime.Serialization.EnumMember(Value = "linux")]
    Linux = 1,
    [System.Runtime.Serialization.EnumMember(Value = "darwin")]
    Darwin = 2,
    [System.Runtime.Serialization.EnumMember(Value = "freebsd")]
    Freebsd = 3,
    [System.Runtime.Serialization.EnumMember(Value = "netbsd")]
    Netbsd = 4,
    [System.Runtime.Serialization.EnumMember(Value = "openbsd")]
    Openbsd = 5,
    [System.Runtime.Serialization.EnumMember(Value = "dragonflybsd")]
    Dragonflybsd = 6,
    [System.Runtime.Serialization.EnumMember(Value = "hpux")]
    Hpux = 7,
    [System.Runtime.Serialization.EnumMember(Value = "aix")]
    Aix = 8,
    [System.Runtime.Serialization.EnumMember(Value = "solaris")]
    Solaris = 9,
    [System.Runtime.Serialization.EnumMember(Value = "z_os")]
    ZOs = 10,
}

