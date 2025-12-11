from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .error_category import ErrorCategory

@dataclass
class ErrorCategoryStats(AdditionalDataHolder, Parsable):
    """
    Error stats by category
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Category
    category: Optional[ErrorCategory] = None
    # Count
    count: Optional[int] = None
    # Percentage of total
    percentage: Optional[float] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ErrorCategoryStats:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ErrorCategoryStats
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ErrorCategoryStats()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .error_category import ErrorCategory

        from .error_category import ErrorCategory

        fields: dict[str, Callable[[Any], None]] = {
            "category": lambda n : setattr(self, 'category', n.get_enum_value(ErrorCategory)),
            "count": lambda n : setattr(self, 'count', n.get_int_value()),
            "percentage": lambda n : setattr(self, 'percentage', n.get_float_value()),
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
        writer.write_enum_value("category", self.category)
        writer.write_int_value("count", self.count)
        writer.write_float_value("percentage", self.percentage)
        writer.write_additional_data_value(self.additional_data)
    

