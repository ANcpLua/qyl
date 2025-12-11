from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import ComposedTypeWrapper, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

@dataclass
class AttributeValue(ComposedTypeWrapper, Parsable):
    """
    Composed type wrapper for classes bool, float, int, list[bool], list[float], list[int], list[str], str
    """
    # Composed type representation for type bool
    attribute_value_boolean: Optional[bool] = None
    # Composed type representation for type float
    attribute_value_double: Optional[float] = None
    # Composed type representation for type int
    attribute_value_int64: Optional[int] = None
    # Composed type representation for type str
    attribute_value_string: Optional[str] = None
    # Composed type representation for type list[bool]
    boolean: Optional[list[bool]] = None
    # Composed type representation for type list[float]
    double: Optional[list[float]] = None
    # Composed type representation for type list[int]
    int64: Optional[list[int]] = None
    # Composed type representation for type list[str]
    string: Optional[list[str]] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> AttributeValue:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: AttributeValue
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        result = AttributeValue()
        if attribute_value_boolean_value := parse_node.get_bool_value():
            result.attribute_value_boolean = attribute_value_boolean_value
        elif attribute_value_double_value := parse_node.get_float_value():
            result.attribute_value_double = attribute_value_double_value
        elif attribute_value_int64_value := parse_node.get_int_value():
            result.attribute_value_int64 = attribute_value_int64_value
        elif attribute_value_string_value := parse_node.get_str_value():
            result.attribute_value_string = attribute_value_string_value
        elif boolean_value := parse_node.get_collection_of_primitive_values(bool):
            result.boolean = boolean_value
        elif double_value := parse_node.get_collection_of_primitive_values(float):
            result.double = double_value
        elif int64_value := parse_node.get_collection_of_primitive_values(int):
            result.int64 = int64_value
        elif string_value := parse_node.get_collection_of_primitive_values(str):
            result.string = string_value
        return result
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        return {}
    
    def serialize(self,writer: SerializationWriter) -> None:
        """
        Serializes information the current object
        param writer: Serialization writer to use to serialize this model
        Returns: None
        """
        if writer is None:
            raise TypeError("writer cannot be null.")
        if self.attribute_value_boolean:
            writer.write_bool_value(None, self.attribute_value_boolean)
        elif self.attribute_value_double:
            writer.write_float_value(None, self.attribute_value_double)
        elif self.attribute_value_int64:
            writer.write_int_value(None, self.attribute_value_int64)
        elif self.attribute_value_string:
            writer.write_str_value(None, self.attribute_value_string)
        elif self.boolean:
            writer.write_collection_of_primitive_values(None, self.boolean)
        elif self.double:
            writer.write_collection_of_primitive_values(None, self.double)
        elif self.int64:
            writer.write_collection_of_primitive_values(None, self.int64)
        elif self.string:
            writer.write_collection_of_primitive_values(None, self.string)
    

