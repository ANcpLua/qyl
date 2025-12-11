from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .error_status import ErrorStatus

@dataclass
class ErrorTypeStats(AdditionalDataHolder, Parsable):
    """
    Error stats by type
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Affected users
    affected_users: Optional[int] = None
    # Count
    count: Optional[int] = None
    # Error type
    error_type: Optional[str] = None
    # Percentage of total
    percentage: Optional[float] = None
    # Status
    status: Optional[ErrorStatus] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ErrorTypeStats:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ErrorTypeStats
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ErrorTypeStats()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .error_status import ErrorStatus

        from .error_status import ErrorStatus

        fields: dict[str, Callable[[Any], None]] = {
            "affected_users": lambda n : setattr(self, 'affected_users', n.get_int_value()),
            "count": lambda n : setattr(self, 'count', n.get_int_value()),
            "error_type": lambda n : setattr(self, 'error_type', n.get_str_value()),
            "percentage": lambda n : setattr(self, 'percentage', n.get_float_value()),
            "status": lambda n : setattr(self, 'status', n.get_enum_value(ErrorStatus)),
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
        writer.write_int_value("affected_users", self.affected_users)
        writer.write_int_value("count", self.count)
        writer.write_str_value("error_type", self.error_type)
        writer.write_float_value("percentage", self.percentage)
        writer.write_enum_value("status", self.status)
        writer.write_additional_data_value(self.additional_data)
    

