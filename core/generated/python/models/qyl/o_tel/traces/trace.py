from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .span import Span

@dataclass
class Trace(AdditionalDataHolder, Parsable):
    """
    Complete trace containing all related spans
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Trace duration in nanoseconds
    duration_ns: Optional[int] = None
    # Trace end time
    end_time: Optional[datetime.datetime] = None
    # Whether trace contains errors
    has_error: Optional[bool] = None
    # Root span of the trace
    root_span: Optional[Span] = None
    # Services involved in this trace
    services: Optional[list[str]] = None
    # Total span count
    span_count: Optional[int] = None
    # All spans in this trace
    spans: Optional[list[Span]] = None
    # Trace start time
    start_time: Optional[datetime.datetime] = None
    # Trace identifier
    trace_id: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> Trace:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: Trace
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return Trace()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .span import Span

        from .span import Span

        fields: dict[str, Callable[[Any], None]] = {
            "duration_ns": lambda n : setattr(self, 'duration_ns', n.get_int_value()),
            "end_time": lambda n : setattr(self, 'end_time', n.get_datetime_value()),
            "has_error": lambda n : setattr(self, 'has_error', n.get_bool_value()),
            "root_span": lambda n : setattr(self, 'root_span', n.get_object_value(Span)),
            "services": lambda n : setattr(self, 'services', n.get_collection_of_primitive_values(str)),
            "span_count": lambda n : setattr(self, 'span_count', n.get_int_value()),
            "spans": lambda n : setattr(self, 'spans', n.get_collection_of_object_values(Span)),
            "start_time": lambda n : setattr(self, 'start_time', n.get_datetime_value()),
            "trace_id": lambda n : setattr(self, 'trace_id', n.get_str_value()),
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
        writer.write_int_value("duration_ns", self.duration_ns)
        writer.write_datetime_value("end_time", self.end_time)
        writer.write_bool_value("has_error", self.has_error)
        writer.write_object_value("root_span", self.root_span)
        writer.write_collection_of_primitive_values("services", self.services)
        writer.write_int_value("span_count", self.span_count)
        writer.write_collection_of_object_values("spans", self.spans)
        writer.write_datetime_value("start_time", self.start_time)
        writer.write_str_value("trace_id", self.trace_id)
        writer.write_additional_data_value(self.additional_data)
    

