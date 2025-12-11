from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .metric_data_point import MetricDataPoint
    from .metric_time_series_labels import MetricTimeSeries_labels

@dataclass
class MetricTimeSeries(AdditionalDataHolder, Parsable):
    """
    Metric time series
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Labels
    labels: Optional[MetricTimeSeries_labels] = None
    # Data points
    points: Optional[list[MetricDataPoint]] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> MetricTimeSeries:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: MetricTimeSeries
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return MetricTimeSeries()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .metric_data_point import MetricDataPoint
        from .metric_time_series_labels import MetricTimeSeries_labels

        from .metric_data_point import MetricDataPoint
        from .metric_time_series_labels import MetricTimeSeries_labels

        fields: dict[str, Callable[[Any], None]] = {
            "labels": lambda n : setattr(self, 'labels', n.get_object_value(MetricTimeSeries_labels)),
            "points": lambda n : setattr(self, 'points', n.get_collection_of_object_values(MetricDataPoint)),
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
        writer.write_object_value("labels", self.labels)
        writer.write_collection_of_object_values("points", self.points)
        writer.write_additional_data_value(self.additional_data)
    

