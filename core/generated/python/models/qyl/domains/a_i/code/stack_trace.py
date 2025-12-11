from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .stack_frame import StackFrame

@dataclass
class StackTrace(AdditionalDataHolder, Parsable):
    """
    Full stack trace
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Stack frames (top to bottom)
    frames: Optional[list[StackFrame]] = None
    # Total frame count before truncation
    total_frames: Optional[int] = None
    # Whether the stack was truncated
    truncated: Optional[bool] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> StackTrace:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: StackTrace
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return StackTrace()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .stack_frame import StackFrame

        from .stack_frame import StackFrame

        fields: dict[str, Callable[[Any], None]] = {
            "frames": lambda n : setattr(self, 'frames', n.get_collection_of_object_values(StackFrame)),
            "total_frames": lambda n : setattr(self, 'total_frames', n.get_int_value()),
            "truncated": lambda n : setattr(self, 'truncated', n.get_bool_value()),
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
        writer.write_collection_of_object_values("frames", self.frames)
        writer.write_int_value("total_frames", self.total_frames)
        writer.write_bool_value("truncated", self.truncated)
        writer.write_additional_data_value(self.additional_data)
    

