from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

@dataclass
class ExceptionServiceStats(AdditionalDataHolder, Parsable):
    """
    Exception stats by service
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Exception count
    count: Optional[int] = None
    # Exception rate (per minute)
    rate_per_minute: Optional[float] = None
    # Service name
    service_name: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ExceptionServiceStats:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ExceptionServiceStats
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ExceptionServiceStats()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        fields: dict[str, Callable[[Any], None]] = {
            "count": lambda n : setattr(self, 'count', n.get_int_value()),
            "rate_per_minute": lambda n : setattr(self, 'rate_per_minute', n.get_float_value()),
            "service_name": lambda n : setattr(self, 'service_name', n.get_str_value()),
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
        writer.write_int_value("count", self.count)
        writer.write_float_value("rate_per_minute", self.rate_per_minute)
        writer.write_str_value("service_name", self.service_name)
        writer.write_additional_data_value(self.additional_data)
    

