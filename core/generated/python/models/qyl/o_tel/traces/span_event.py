from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from ...common.attribute import Attribute

@dataclass
class SpanEvent(AdditionalDataHolder, Parsable):
    """
    Event occurring during a span's lifetime
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Event attributes
    attributes: Optional[list[Attribute]] = None
    # Dropped attributes count
    dropped_attributes_count: Optional[int] = None
    # Event name
    name: Optional[str] = None
    # Event timestamp in nanoseconds since epoch
    time_unix_nano: Optional[int] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> SpanEvent:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: SpanEvent
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return SpanEvent()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from ...common.attribute import Attribute

        from ...common.attribute import Attribute

        fields: dict[str, Callable[[Any], None]] = {
            "attributes": lambda n : setattr(self, 'attributes', n.get_collection_of_object_values(Attribute)),
            "dropped_attributes_count": lambda n : setattr(self, 'dropped_attributes_count', n.get_int_value()),
            "name": lambda n : setattr(self, 'name', n.get_str_value()),
            "time_unix_nano": lambda n : setattr(self, 'time_unix_nano', n.get_int_value()),
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
        writer.write_int_value("dropped_attributes_count", self.dropped_attributes_count)
        writer.write_str_value("name", self.name)
        writer.write_int_value("time_unix_nano", self.time_unix_nano)
        writer.write_additional_data_value(self.additional_data)
    

