from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .attribute import Attribute

@dataclass
class InstrumentationScope(AdditionalDataHolder, Parsable):
    """
    Instrumentation scope identifying the library/component emitting telemetry
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Additional attributes for the scope
    attributes: Optional[list[Attribute]] = None
    # Dropped attributes count
    dropped_attributes_count: Optional[int] = None
    # Name of the instrumentation scope (library name)
    name: Optional[str] = None
    # Version of the instrumentation scope
    version: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> InstrumentationScope:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: InstrumentationScope
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return InstrumentationScope()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .attribute import Attribute

        from .attribute import Attribute

        fields: dict[str, Callable[[Any], None]] = {
            "attributes": lambda n : setattr(self, 'attributes', n.get_collection_of_object_values(Attribute)),
            "dropped_attributes_count": lambda n : setattr(self, 'dropped_attributes_count', n.get_int_value()),
            "name": lambda n : setattr(self, 'name', n.get_str_value()),
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
        writer.write_collection_of_object_values("attributes", self.attributes)
        writer.write_int_value("dropped_attributes_count", self.dropped_attributes_count)
        writer.write_str_value("name", self.name)
        writer.write_str_value("version", self.version)
        writer.write_additional_data_value(self.additional_data)
    

