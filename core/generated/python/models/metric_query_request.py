from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .metric_query_request_filters import MetricQueryRequest_filters
    from .qyl.common.pagination.time_bucket import TimeBucket
    from .qyl.o_tel.metrics.aggregation_function import AggregationFunction

@dataclass
class MetricQueryRequest(AdditionalDataHolder, Parsable):
    """
    Metric query request
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Aggregation function
    aggregation: Optional[AggregationFunction] = None
    # End time
    end_time: Optional[datetime.datetime] = None
    # Label filters
    filters: Optional[MetricQueryRequest_filters] = None
    # Group by labels
    group_by: Optional[list[str]] = None
    # Metric name
    metric_name: Optional[str] = None
    # Start time
    start_time: Optional[datetime.datetime] = None
    # Step interval
    step: Optional[TimeBucket] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> MetricQueryRequest:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: MetricQueryRequest
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return MetricQueryRequest()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .metric_query_request_filters import MetricQueryRequest_filters
        from .qyl.common.pagination.time_bucket import TimeBucket
        from .qyl.o_tel.metrics.aggregation_function import AggregationFunction

        from .metric_query_request_filters import MetricQueryRequest_filters
        from .qyl.common.pagination.time_bucket import TimeBucket
        from .qyl.o_tel.metrics.aggregation_function import AggregationFunction

        fields: dict[str, Callable[[Any], None]] = {
            "aggregation": lambda n : setattr(self, 'aggregation', n.get_enum_value(AggregationFunction)),
            "end_time": lambda n : setattr(self, 'end_time', n.get_datetime_value()),
            "filters": lambda n : setattr(self, 'filters', n.get_object_value(MetricQueryRequest_filters)),
            "group_by": lambda n : setattr(self, 'group_by', n.get_collection_of_primitive_values(str)),
            "metric_name": lambda n : setattr(self, 'metric_name', n.get_str_value()),
            "start_time": lambda n : setattr(self, 'start_time', n.get_datetime_value()),
            "step": lambda n : setattr(self, 'step', n.get_enum_value(TimeBucket)),
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
        writer.write_enum_value("aggregation", self.aggregation)
        writer.write_datetime_value("end_time", self.end_time)
        writer.write_object_value("filters", self.filters)
        writer.write_collection_of_primitive_values("group_by", self.group_by)
        writer.write_str_value("metric_name", self.metric_name)
        writer.write_datetime_value("start_time", self.start_time)
        writer.write_enum_value("step", self.step)
        writer.write_additional_data_value(self.additional_data)
    

