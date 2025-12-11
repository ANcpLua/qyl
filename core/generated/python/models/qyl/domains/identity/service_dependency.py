from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

@dataclass
class ServiceDependency(AdditionalDataHolder, Parsable):
    """
    Service dependency map
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Average latency in milliseconds
    avg_latency_ms: Optional[float] = None
    # Error rate
    error_rate: Optional[float] = None
    # Request count
    request_count: Optional[int] = None
    # Source service
    source_service: Optional[str] = None
    # Target service
    target_service: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ServiceDependency:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ServiceDependency
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ServiceDependency()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        fields: dict[str, Callable[[Any], None]] = {
            "avg_latency_ms": lambda n : setattr(self, 'avg_latency_ms', n.get_float_value()),
            "error_rate": lambda n : setattr(self, 'error_rate', n.get_float_value()),
            "request_count": lambda n : setattr(self, 'request_count', n.get_int_value()),
            "source_service": lambda n : setattr(self, 'source_service', n.get_str_value()),
            "target_service": lambda n : setattr(self, 'target_service', n.get_str_value()),
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
        writer.write_int_value("request_count", self.request_count)
        writer.write_str_value("source_service", self.source_service)
        writer.write_str_value("target_service", self.target_service)
        writer.write_additional_data_value(self.additional_data)
    

