from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .exception_status import ExceptionStatus

@dataclass
class ExceptionTypeStats(AdditionalDataHolder, Parsable):
    """
    Exception stats by type
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Count
    count: Optional[int] = None
    # Exception type
    exception_type: Optional[str] = None
    # Percentage of total
    percentage: Optional[float] = None
    # Status
    status: Optional[ExceptionStatus] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ExceptionTypeStats:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ExceptionTypeStats
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ExceptionTypeStats()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .exception_status import ExceptionStatus

        from .exception_status import ExceptionStatus

        fields: dict[str, Callable[[Any], None]] = {
            "count": lambda n : setattr(self, 'count', n.get_int_value()),
            "exception_type": lambda n : setattr(self, 'exception_type', n.get_str_value()),
            "percentage": lambda n : setattr(self, 'percentage', n.get_float_value()),
            "status": lambda n : setattr(self, 'status', n.get_enum_value(ExceptionStatus)),
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
        writer.write_str_value("exception_type", self.exception_type)
        writer.write_float_value("percentage", self.percentage)
        writer.write_enum_value("status", self.status)
        writer.write_additional_data_value(self.additional_data)
    

