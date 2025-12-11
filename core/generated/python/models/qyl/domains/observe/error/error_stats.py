from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .error_category_stats import ErrorCategoryStats
    from .error_service_stats import ErrorServiceStats
    from .error_trend import ErrorTrend
    from .error_type_stats import ErrorTypeStats

@dataclass
class ErrorStats(AdditionalDataHolder, Parsable):
    """
    Error statistics
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Errors by category
    by_category: Optional[list[ErrorCategoryStats]] = None
    # Errors by service
    by_service: Optional[list[ErrorServiceStats]] = None
    # Error rate
    error_rate: Optional[float] = None
    # Top errors
    top_errors: Optional[list[ErrorTypeStats]] = None
    # Total error count
    total_count: Optional[int] = None
    # Trend
    trend: Optional[ErrorTrend] = None
    # Unique error types
    unique_types: Optional[int] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ErrorStats:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ErrorStats
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ErrorStats()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .error_category_stats import ErrorCategoryStats
        from .error_service_stats import ErrorServiceStats
        from .error_trend import ErrorTrend
        from .error_type_stats import ErrorTypeStats

        from .error_category_stats import ErrorCategoryStats
        from .error_service_stats import ErrorServiceStats
        from .error_trend import ErrorTrend
        from .error_type_stats import ErrorTypeStats

        fields: dict[str, Callable[[Any], None]] = {
            "by_category": lambda n : setattr(self, 'by_category', n.get_collection_of_object_values(ErrorCategoryStats)),
            "by_service": lambda n : setattr(self, 'by_service', n.get_collection_of_object_values(ErrorServiceStats)),
            "error_rate": lambda n : setattr(self, 'error_rate', n.get_float_value()),
            "top_errors": lambda n : setattr(self, 'top_errors', n.get_collection_of_object_values(ErrorTypeStats)),
            "total_count": lambda n : setattr(self, 'total_count', n.get_int_value()),
            "trend": lambda n : setattr(self, 'trend', n.get_enum_value(ErrorTrend)),
            "unique_types": lambda n : setattr(self, 'unique_types', n.get_int_value()),
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
        writer.write_collection_of_object_values("by_category", self.by_category)
        writer.write_collection_of_object_values("by_service", self.by_service)
        writer.write_float_value("error_rate", self.error_rate)
        writer.write_collection_of_object_values("top_errors", self.top_errors)
        writer.write_int_value("total_count", self.total_count)
        writer.write_enum_value("trend", self.trend)
        writer.write_int_value("unique_types", self.unique_types)
        writer.write_additional_data_value(self.additional_data)
    

