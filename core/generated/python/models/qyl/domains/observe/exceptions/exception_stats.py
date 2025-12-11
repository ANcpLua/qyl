from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .exception_service_stats import ExceptionServiceStats
    from .exception_trend import ExceptionTrend
    from .exception_type_stats import ExceptionTypeStats

@dataclass
class ExceptionStats(AdditionalDataHolder, Parsable):
    """
    Exception statistics
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Most affected services
    by_service: Optional[list[ExceptionServiceStats]] = None
    # Exceptions by type
    by_type: Optional[list[ExceptionTypeStats]] = None
    # Total exception count
    total_count: Optional[int] = None
    # Exception trend (up/down/stable)
    trend: Optional[ExceptionTrend] = None
    # Unique exception types
    unique_types: Optional[int] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ExceptionStats:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ExceptionStats
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ExceptionStats()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .exception_service_stats import ExceptionServiceStats
        from .exception_trend import ExceptionTrend
        from .exception_type_stats import ExceptionTypeStats

        from .exception_service_stats import ExceptionServiceStats
        from .exception_trend import ExceptionTrend
        from .exception_type_stats import ExceptionTypeStats

        fields: dict[str, Callable[[Any], None]] = {
            "by_service": lambda n : setattr(self, 'by_service', n.get_collection_of_object_values(ExceptionServiceStats)),
            "by_type": lambda n : setattr(self, 'by_type', n.get_collection_of_object_values(ExceptionTypeStats)),
            "total_count": lambda n : setattr(self, 'total_count', n.get_int_value()),
            "trend": lambda n : setattr(self, 'trend', n.get_enum_value(ExceptionTrend)),
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
        writer.write_collection_of_object_values("by_service", self.by_service)
        writer.write_collection_of_object_values("by_type", self.by_type)
        writer.write_int_value("total_count", self.total_count)
        writer.write_enum_value("trend", self.trend)
        writer.write_int_value("unique_types", self.unique_types)
        writer.write_additional_data_value(self.additional_data)
    

