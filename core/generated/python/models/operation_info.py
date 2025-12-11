from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

@dataclass
class OperationInfo(AdditionalDataHolder, Parsable):
    """
    Operation information
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Average duration in milliseconds
    avg_duration_ms: Optional[float] = None
    # Error count
    error_count: Optional[int] = None
    # Operation name
    name: Optional[str] = None
    # P99 duration in milliseconds
    p99_duration_ms: Optional[float] = None
    # Request count
    request_count: Optional[int] = None
    # Span kind
    span_kind: Optional[float] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> OperationInfo:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: OperationInfo
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return OperationInfo()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        fields: dict[str, Callable[[Any], None]] = {
            "avg_duration_ms": lambda n : setattr(self, 'avg_duration_ms', n.get_float_value()),
            "error_count": lambda n : setattr(self, 'error_count', n.get_int_value()),
            "name": lambda n : setattr(self, 'name', n.get_str_value()),
            "p99_duration_ms": lambda n : setattr(self, 'p99_duration_ms', n.get_float_value()),
            "request_count": lambda n : setattr(self, 'request_count', n.get_int_value()),
            "span_kind": lambda n : setattr(self, 'span_kind', n.get_float_value()),
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
        writer.write_float_value("avg_duration_ms", self.avg_duration_ms)
        writer.write_int_value("error_count", self.error_count)
        writer.write_str_value("name", self.name)
        writer.write_float_value("p99_duration_ms", self.p99_duration_ms)
        writer.write_int_value("request_count", self.request_count)
        writer.write_float_value("span_kind", self.span_kind)
        writer.write_additional_data_value(self.additional_data)
    

