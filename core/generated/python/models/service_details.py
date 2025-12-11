from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .qyl.common.attribute import Attribute
    from .qyl.common.instrumentation_scope import InstrumentationScope

@dataclass
class ServiceDetails(AdditionalDataHolder, Parsable):
    """
    Service details
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Average latency in milliseconds
    avg_latency_ms: Optional[float] = None
    # Error rate
    error_rate: Optional[float] = None
    # Instance count
    instance_count: Optional[int] = None
    # Instrumentation libraries
    instrumentation_libraries: Optional[list[InstrumentationScope]] = None
    # Last seen
    last_seen: Optional[datetime.datetime] = None
    # Service name
    name: Optional[str] = None
    # Service namespace
    namespace_name: Optional[str] = None
    # P99 latency in milliseconds
    p99_latency_ms: Optional[float] = None
    # Request rate (per second)
    request_rate: Optional[float] = None
    # Resource attributes
    resource_attributes: Optional[list[Attribute]] = None
    # Service version
    version: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ServiceDetails:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ServiceDetails
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ServiceDetails()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .qyl.common.attribute import Attribute
        from .qyl.common.instrumentation_scope import InstrumentationScope

        from .qyl.common.attribute import Attribute
        from .qyl.common.instrumentation_scope import InstrumentationScope

        fields: dict[str, Callable[[Any], None]] = {
            "avg_latency_ms": lambda n : setattr(self, 'avg_latency_ms', n.get_float_value()),
            "error_rate": lambda n : setattr(self, 'error_rate', n.get_float_value()),
            "instance_count": lambda n : setattr(self, 'instance_count', n.get_int_value()),
            "instrumentation_libraries": lambda n : setattr(self, 'instrumentation_libraries', n.get_collection_of_object_values(InstrumentationScope)),
            "last_seen": lambda n : setattr(self, 'last_seen', n.get_datetime_value()),
            "name": lambda n : setattr(self, 'name', n.get_str_value()),
            "namespace_name": lambda n : setattr(self, 'namespace_name', n.get_str_value()),
            "p99_latency_ms": lambda n : setattr(self, 'p99_latency_ms', n.get_float_value()),
            "request_rate": lambda n : setattr(self, 'request_rate', n.get_float_value()),
            "resource_attributes": lambda n : setattr(self, 'resource_attributes', n.get_collection_of_object_values(Attribute)),
            "version": lambda n : setattr(self, 'version', n.get_str_value()),
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
        writer.write_float_value("avg_latency_ms", self.avg_latency_ms)
        writer.write_float_value("error_rate", self.error_rate)
        writer.write_int_value("instance_count", self.instance_count)
        writer.write_collection_of_object_values("instrumentation_libraries", self.instrumentation_libraries)
        writer.write_datetime_value("last_seen", self.last_seen)
        writer.write_str_value("name", self.name)
        writer.write_str_value("namespace_name", self.namespace_name)
        writer.write_float_value("p99_latency_ms", self.p99_latency_ms)
        writer.write_float_value("request_rate", self.request_rate)
        writer.write_collection_of_object_values("resource_attributes", self.resource_attributes)
        writer.write_str_value("version", self.version)
        writer.write_additional_data_value(self.additional_data)
    

