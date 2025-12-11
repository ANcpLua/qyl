from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .attribute_value import AttributeValue

@dataclass
class Attribute(AdditionalDataHolder, Parsable):
    """
    Key-value attribute pair following OTel conventions
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Attribute key (dot-separated namespace)
    key: Optional[str] = None
    # Attribute value
    value: Optional[AttributeValue] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> Attribute:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: Attribute
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return Attribute()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .attribute_value import AttributeValue

        from .attribute_value import AttributeValue

        fields: dict[str, Callable[[Any], None]] = {
            "key": lambda n : setattr(self, 'key', n.get_str_value()),
            "value": lambda n : setattr(self, 'value', n.get_object_value(AttributeValue)),
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
        writer.write_str_value("key", self.key)
        writer.write_object_value("value", self.value)
        writer.write_additional_data_value(self.additional_data)
    

