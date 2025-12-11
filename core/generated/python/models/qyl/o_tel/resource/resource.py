from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from ...common.attribute import Attribute
    from ..enums.telemetry_sdk_language import TelemetrySdkLanguage
    from .cloud_provider import CloudProvider
    from .host_arch import HostArch
    from .os_type import OsType

@dataclass
class Resource(AdditionalDataHolder, Parsable):
    """
    Resource describes the entity producing telemetry
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Additional resource attributes
    attributes: Optional[list[Attribute]] = None
    # Cloud account ID
    cloud_account_id: Optional[str] = None
    # Cloud availability zone
    cloud_availability_zone: Optional[str] = None
    # Cloud platform (e.g., aws_ecs, gcp_cloud_run)
    cloud_platform: Optional[str] = None
    # Cloud provider
    cloud_provider: Optional[CloudProvider] = None
    # Cloud region
    cloud_region: Optional[str] = None
    # Container ID
    container_id: Optional[str] = None
    # Container image name
    container_image_name: Optional[str] = None
    # Container image tag
    container_image_tag: Optional[str] = None
    # Container name
    container_name: Optional[str] = None
    # Deployment environment (e.g., production, staging)
    deployment_environment_name: Optional[str] = None
    # Dropped attributes count
    dropped_attributes_count: Optional[int] = None
    # Host architecture (e.g., amd64, arm64)
    host_arch: Optional[HostArch] = None
    # Host ID
    host_id: Optional[str] = None
    # Host name
    host_name: Optional[str] = None
    # Host type (e.g., n1-standard-1)
    host_type: Optional[str] = None
    # Kubernetes cluster name
    k8s_cluster_name: Optional[str] = None
    # Kubernetes deployment name
    k8s_deployment_name: Optional[str] = None
    # Kubernetes namespace
    k8s_namespace_name: Optional[str] = None
    # Kubernetes pod name
    k8s_pod_name: Optional[str] = None
    # Kubernetes pod UID
    k8s_pod_uid: Optional[str] = None
    # Operating system description
    os_description: Optional[str] = None
    # Operating system type
    os_type: Optional[OsType] = None
    # Operating system version
    os_version: Optional[str] = None
    # Process command line
    process_command_line: Optional[str] = None
    # Process executable name
    process_executable_name: Optional[str] = None
    # Process ID
    process_pid: Optional[int] = None
    # Process runtime name
    process_runtime_name: Optional[str] = None
    # Process runtime version
    process_runtime_version: Optional[str] = None
    # Service instance ID (unique per instance)
    service_instance_id: Optional[str] = None
    # Service name (required)
    service_name: Optional[str] = None
    # Service namespace for grouping
    service_namespace: Optional[str] = None
    # Service version
    service_version: Optional[str] = None
    # Auto-instrumentation agent name
    telemetry_auto_version: Optional[str] = None
    # Telemetry SDK language
    telemetry_sdk_language: Optional[TelemetrySdkLanguage] = None
    # Telemetry SDK name
    telemetry_sdk_name: Optional[str] = None
    # Telemetry SDK version
    telemetry_sdk_version: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> Resource:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: Resource
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return Resource()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from ...common.attribute import Attribute
        from ..enums.telemetry_sdk_language import TelemetrySdkLanguage
        from .cloud_provider import CloudProvider
        from .host_arch import HostArch
        from .os_type import OsType

        from ...common.attribute import Attribute
        from ..enums.telemetry_sdk_language import TelemetrySdkLanguage
        from .cloud_provider import CloudProvider
        from .host_arch import HostArch
        from .os_type import OsType

        fields: dict[str, Callable[[Any], None]] = {
            "attributes": lambda n : setattr(self, 'attributes', n.get_collection_of_object_values(Attribute)),
            "cloud.account.id": lambda n : setattr(self, 'cloud_account_id', n.get_str_value()),
            "cloud.availability_zone": lambda n : setattr(self, 'cloud_availability_zone', n.get_str_value()),
            "cloud.platform": lambda n : setattr(self, 'cloud_platform', n.get_str_value()),
            "cloud.provider": lambda n : setattr(self, 'cloud_provider', n.get_enum_value(CloudProvider)),
            "cloud.region": lambda n : setattr(self, 'cloud_region', n.get_str_value()),
            "container.id": lambda n : setattr(self, 'container_id', n.get_str_value()),
            "container.image.name": lambda n : setattr(self, 'container_image_name', n.get_str_value()),
            "container.image.tag": lambda n : setattr(self, 'container_image_tag', n.get_str_value()),
            "container.name": lambda n : setattr(self, 'container_name', n.get_str_value()),
            "deployment.environment.name": lambda n : setattr(self, 'deployment_environment_name', n.get_str_value()),
            "dropped_attributes_count": lambda n : setattr(self, 'dropped_attributes_count', n.get_int_value()),
            "host.arch": lambda n : setattr(self, 'host_arch', n.get_enum_value(HostArch)),
            "host.id": lambda n : setattr(self, 'host_id', n.get_str_value()),
            "host.name": lambda n : setattr(self, 'host_name', n.get_str_value()),
            "host.type": lambda n : setattr(self, 'host_type', n.get_str_value()),
            "k8s.cluster.name": lambda n : setattr(self, 'k8s_cluster_name', n.get_str_value()),
            "k8s.deployment.name": lambda n : setattr(self, 'k8s_deployment_name', n.get_str_value()),
            "k8s.namespace.name": lambda n : setattr(self, 'k8s_namespace_name', n.get_str_value()),
            "k8s.pod.name": lambda n : setattr(self, 'k8s_pod_name', n.get_str_value()),
            "k8s.pod.uid": lambda n : setattr(self, 'k8s_pod_uid', n.get_str_value()),
            "os.description": lambda n : setattr(self, 'os_description', n.get_str_value()),
            "os.type": lambda n : setattr(self, 'os_type', n.get_enum_value(OsType)),
            "os.version": lambda n : setattr(self, 'os_version', n.get_str_value()),
            "process.command_line": lambda n : setattr(self, 'process_command_line', n.get_str_value()),
            "process.executable.name": lambda n : setattr(self, 'process_executable_name', n.get_str_value()),
            "process.pid": lambda n : setattr(self, 'process_pid', n.get_int_value()),
            "process.runtime.name": lambda n : setattr(self, 'process_runtime_name', n.get_str_value()),
            "process.runtime.version": lambda n : setattr(self, 'process_runtime_version', n.get_str_value()),
            "service.instance.id": lambda n : setattr(self, 'service_instance_id', n.get_str_value()),
            "service.name": lambda n : setattr(self, 'service_name', n.get_str_value()),
            "service.namespace": lambda n : setattr(self, 'service_namespace', n.get_str_value()),
            "service.version": lambda n : setattr(self, 'service_version', n.get_str_value()),
            "telemetry.auto.version": lambda n : setattr(self, 'telemetry_auto_version', n.get_str_value()),
            "telemetry.sdk.language": lambda n : setattr(self, 'telemetry_sdk_language', n.get_enum_value(TelemetrySdkLanguage)),
            "telemetry.sdk.name": lambda n : setattr(self, 'telemetry_sdk_name', n.get_str_value()),
            "telemetry.sdk.version": lambda n : setattr(self, 'telemetry_sdk_version', n.get_str_value()),
        }
        return fields
    
    def serialize(self,writer: SerializationWriter) -> None:
        """
        Serializes information the current object
        param writer: Serialization writer to use to serialize this model
        Returns: None
        """
        if writer is None:
            raise TypeError("writer cannot be null.")
        writer.write_collection_of_object_values("attributes", self.attributes)
        writer.write_str_value("cloud.account.id", self.cloud_account_id)
        writer.write_str_value("cloud.availability_zone", self.cloud_availability_zone)
        writer.write_str_value("cloud.platform", self.cloud_platform)
        writer.write_enum_value("cloud.provider", self.cloud_provider)
        writer.write_str_value("cloud.region", self.cloud_region)
        writer.write_str_value("container.id", self.container_id)
        writer.write_str_value("container.image.name", self.container_image_name)
        writer.write_str_value("container.image.tag", self.container_image_tag)
        writer.write_str_value("container.name", self.container_name)
        writer.write_str_value("deployment.environment.name", self.deployment_environment_name)
        writer.write_int_value("dropped_attributes_count", self.dropped_attributes_count)
        writer.write_enum_value("host.arch", self.host_arch)
        writer.write_str_value("host.id", self.host_id)
        writer.write_str_value("host.name", self.host_name)
        writer.write_str_value("host.type", self.host_type)
        writer.write_str_value("k8s.cluster.name", self.k8s_cluster_name)
        writer.write_str_value("k8s.deployment.name", self.k8s_deployment_name)
        writer.write_str_value("k8s.namespace.name", self.k8s_namespace_name)
        writer.write_str_value("k8s.pod.name", self.k8s_pod_name)
        writer.write_str_value("k8s.pod.uid", self.k8s_pod_uid)
        writer.write_str_value("os.description", self.os_description)
        writer.write_enum_value("os.type", self.os_type)
        writer.write_str_value("os.version", self.os_version)
        writer.write_str_value("process.command_line", self.process_command_line)
        writer.write_str_value("process.executable.name", self.process_executable_name)
        writer.write_int_value("process.pid", self.process_pid)
        writer.write_str_value("process.runtime.name", self.process_runtime_name)
        writer.write_str_value("process.runtime.version", self.process_runtime_version)
        writer.write_str_value("service.instance.id", self.service_instance_id)
        writer.write_str_value("service.name", self.service_name)
        writer.write_str_value("service.namespace", self.service_namespace)
        writer.write_str_value("service.version", self.service_version)
        writer.write_str_value("telemetry.auto.version", self.telemetry_auto_version)
        writer.write_enum_value("telemetry.sdk.language", self.telemetry_sdk_language)
        writer.write_str_value("telemetry.sdk.name", self.telemetry_sdk_name)
        writer.write_str_value("telemetry.sdk.version", self.telemetry_sdk_version)
        writer.write_additional_data_value(self.additional_data)
    

