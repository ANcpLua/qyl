from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .log_aggregation_bucket import LogAggregationBucket

@dataclass
class LogAggregationResponse(AdditionalDataHolder, Parsable):
    """
    Log aggregation response
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Aggregation results
    results: Optional[list[LogAggregationBucket]] = None
    # Total matching logs
    total_count: Optional[int] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> LogAggregationResponse:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: LogAggregationResponse
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return LogAggregationResponse()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .log_aggregation_bucket import LogAggregationBucket

        from .log_aggregation_bucket import LogAggregationBucket

        fields: dict[str, Callable[[Any], None]] = {
            "results": lambda n : setattr(self, 'results', n.get_collection_of_object_values(LogAggregationBucket)),
            "total_count": lambda n : setattr(self, 'total_count', n.get_int_value()),
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
        writer.write_collection_of_object_values("results", self.results)
        writer.write_int_value("total_count", self.total_count)
        writer.write_additional_data_value(self.additional_data)
    

