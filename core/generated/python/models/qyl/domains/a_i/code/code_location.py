from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

@dataclass
class CodeLocation(AdditionalDataHolder, Parsable):
    """
    Precise source code location for debugging and tracing
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Class/type name
    class_name: Optional[str] = None
    # Column number (1-indexed)
    column_number: Optional[int] = None
    # Source file path
    filepath: Optional[str] = None
    # Function/method name
    function_name: Optional[str] = None
    # Line number (1-indexed)
    line_number: Optional[int] = None
    # Namespace/module
    namespace: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> CodeLocation:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: CodeLocation
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return CodeLocation()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        fields: dict[str, Callable[[Any], None]] = {
            "class_name": lambda n : setattr(self, 'class_name', n.get_str_value()),
            "column_number": lambda n : setattr(self, 'column_number', n.get_int_value()),
            "filepath": lambda n : setattr(self, 'filepath', n.get_str_value()),
            "function_name": lambda n : setattr(self, 'function_name', n.get_str_value()),
            "line_number": lambda n : setattr(self, 'line_number', n.get_int_value()),
            "namespace": lambda n : setattr(self, 'namespace', n.get_str_value()),
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
        writer.write_str_value("class_name", self.class_name)
        writer.write_int_value("column_number", self.column_number)
        writer.write_str_value("filepath", self.filepath)
        writer.write_str_value("function_name", self.function_name)
        writer.write_int_value("line_number", self.line_number)
        writer.write_str_value("namespace", self.namespace)
        writer.write_additional_data_value(self.additional_data)
    

