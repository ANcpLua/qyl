from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .pipeline_status_stats import PipelineStatusStats

@dataclass
class PipelineStats(AdditionalDataHolder, Parsable):
    """
    Pipeline statistics
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Average duration in seconds
    avg_duration_seconds: Optional[float] = None
    # Runs by status
    by_status: Optional[list[PipelineStatusStats]] = None
    # P95 duration in seconds
    p95_duration_seconds: Optional[float] = None
    # Success rate
    success_rate: Optional[float] = None
    # Total runs
    total_runs: Optional[int] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> PipelineStats:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: PipelineStats
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return PipelineStats()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .pipeline_status_stats import PipelineStatusStats

        from .pipeline_status_stats import PipelineStatusStats

        fields: dict[str, Callable[[Any], None]] = {
            "avg_duration_seconds": lambda n : setattr(self, 'avg_duration_seconds', n.get_float_value()),
            "by_status": lambda n : setattr(self, 'by_status', n.get_collection_of_object_values(PipelineStatusStats)),
            "p95_duration_seconds": lambda n : setattr(self, 'p95_duration_seconds', n.get_float_value()),
            "success_rate": lambda n : setattr(self, 'success_rate', n.get_float_value()),
            "total_runs": lambda n : setattr(self, 'total_runs', n.get_int_value()),
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
        writer.write_float_value("avg_duration_seconds", self.avg_duration_seconds)
        writer.write_collection_of_object_values("by_status", self.by_status)
        writer.write_float_value("p95_duration_seconds", self.p95_duration_seconds)
        writer.write_float_value("success_rate", self.success_rate)
        writer.write_int_value("total_runs", self.total_runs)
        writer.write_additional_data_value(self.additional_data)
    

