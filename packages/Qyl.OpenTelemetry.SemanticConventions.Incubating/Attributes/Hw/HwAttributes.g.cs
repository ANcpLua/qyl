

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Hw;

public static class HwAttributes
{
    public const string BatteryCapacity = "hw.battery.capacity";

    public const string BatteryChemistry = "hw.battery.chemistry";

    public const string BatteryState = "hw.battery.state";

    public static class BatteryStateValues
    {
        public const string Charging = "charging";

        public const string Discharging = "discharging";
    }

    public const string BiosVersion = "hw.bios_version";

    public const string DriverVersion = "hw.driver_version";

    public const string EnclosureType = "hw.enclosure.type";

    public const string FirmwareVersion = "hw.firmware_version";

    public const string GpuTask = "hw.gpu.task";

    public static class GpuTaskValues
    {
        public const string Decoder = "decoder";

        public const string Encoder = "encoder";

        public const string General = "general";
    }

    public const string Id = "hw.id";

    public const string LimitType = "hw.limit_type";

    public static class LimitTypeValues
    {
        public const string Critical = "critical";

        public const string Degraded = "degraded";

        public const string HighCritical = "high.critical";

        public const string HighDegraded = "high.degraded";

        public const string LowCritical = "low.critical";

        public const string LowDegraded = "low.degraded";

        public const string Max = "max";

        public const string Throttled = "throttled";

        public const string Turbo = "turbo";
    }

    public const string LogicalDiskRaidLevel = "hw.logical_disk.raid_level";

    public const string LogicalDiskState = "hw.logical_disk.state";

    public static class LogicalDiskStateValues
    {
        public const string Free = "free";

        public const string Used = "used";
    }

    public const string MemoryType = "hw.memory.type";

    public const string Model = "hw.model";

    public const string Name = "hw.name";

    public const string NetworkLogicalAddresses = "hw.network.logical_addresses";

    public const string NetworkPhysicalAddress = "hw.network.physical_address";

    public const string Parent = "hw.parent";

    public const string PhysicalDiskSmartAttribute = "hw.physical_disk.smart_attribute";

    public const string PhysicalDiskState = "hw.physical_disk.state";

    public static class PhysicalDiskStateValues
    {
        public const string Remaining = "remaining";
    }

    public const string PhysicalDiskType = "hw.physical_disk.type";

    public const string SensorLocation = "hw.sensor_location";

    public const string SerialNumber = "hw.serial_number";

    public const string State = "hw.state";

    public static class StateValues
    {
        public const string Degraded = "degraded";

        public const string Failed = "failed";

        public const string NeedsCleaning = "needs_cleaning";

        public const string Ok = "ok";

        public const string PredictedFailure = "predicted_failure";
    }

    public const string TapeDriveOperationType = "hw.tape_drive.operation_type";

    public static class TapeDriveOperationTypeValues
    {
        public const string Clean = "clean";

        public const string Mount = "mount";

        public const string Unmount = "unmount";
    }

    public const string Type = "hw.type";

    public static class TypeValues
    {
        public const string Battery = "battery";

        public const string Cpu = "cpu";

        public const string DiskController = "disk_controller";

        public const string Enclosure = "enclosure";

        public const string Fan = "fan";

        public const string Gpu = "gpu";

        public const string LogicalDisk = "logical_disk";

        public const string Memory = "memory";

        public const string Network = "network";

        public const string PhysicalDisk = "physical_disk";

        public const string PowerSupply = "power_supply";

        public const string TapeDrive = "tape_drive";

        public const string Temperature = "temperature";

        public const string Voltage = "voltage";
    }

    public const string Vendor = "hw.vendor";
}
