from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .code_location import CodeLocation

@dataclass
class StackFrame(AdditionalDataHolder, Parsable):
    """
    Single frame in a call stack
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Frame index (0 = top of stack)
    index: Optional[int] = None
    # Native/managed indicator
    is_native: Optional[bool] = None
    # Whether this is user code (not library/framework)
    is_user_code: Optional[bool] = None
    # Source location
    location: Optional[CodeLocation] = None
    # Assembly/module name
    module_name: Optional[str] = None
    # Assembly/module version
    module_version: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> StackFrame:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: StackFrame
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return StackFrame()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .code_location import CodeLocation

        from .code_location import CodeLocation

        fields: dict[str, Callable[[Any], None]] = {
            "index": lambda n : setattr(self, 'index', n.get_int_value()),
            "is_native": lambda n : setattr(self, 'is_native', n.get_bool_value()),
            "is_user_code": lambda n : setattr(self, 'is_user_code', n.get_bool_value()),
            "location": lambda n : setattr(self, 'location', n.get_object_value(CodeLocation)),
            "module_name": lambda n : setattr(self, 'module_name', n.get_str_value()),
            "module_version": lambda n : setattr(self, 'module_version', n.get_str_value()),
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
        writer.write_int_value("index", self.index)
        writer.write_bool_value("is_native", self.is_native)
        writer.write_bool_value("is_user_code", self.is_user_code)
        writer.write_object_value("location", self.location)
        writer.write_str_value("module_name", self.module_name)
        writer.write_str_value("module_version", self.module_version)
        writer.write_additional_data_value(self.additional_data)
    

