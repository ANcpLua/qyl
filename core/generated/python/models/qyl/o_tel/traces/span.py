from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from ...common.attribute import Attribute
    from ...common.instrumentation_scope import InstrumentationScope
    from ..resource.resource import Resource
    from .span_event import SpanEvent
    from .span_link import SpanLink
    from .span_status import SpanStatus

@dataclass
class Span(AdditionalDataHolder, Parsable):
    """
    OpenTelemetry Span representing a single operation in a distributed trace
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Span attributes
    attributes: Optional[list[Attribute]] = None
    # Dropped attributes count
    dropped_attributes_count: Optional[int] = None
    # Dropped events count
    dropped_events_count: Optional[int] = None
    # Dropped links count
    dropped_links_count: Optional[int] = None
    # End timestamp in nanoseconds since epoch
    end_time_unix_nano: Optional[int] = None
    # Span events (logs attached to span)
    events: Optional[list[SpanEvent]] = None
    # Span flags
    flags: Optional[int] = None
    # Instrumentation scope
    instrumentation_scope: Optional[InstrumentationScope] = None
    # Span kind
    kind: Optional[float] = None
    # Links to other spans
    links: Optional[list[SpanLink]] = None
    # Human-readable span name
    name: Optional[str] = None
    # Parent span identifier (null for root spans)
    parent_span_id: Optional[str] = None
    # Resource describing the entity that produced this span
    resource: Optional[Resource] = None
    # Unique span identifier (16 hex chars)
    span_id: Optional[str] = None
    # Start timestamp in nanoseconds since epoch
    start_time_unix_nano: Optional[int] = None
    # Span status
    status: Optional[SpanStatus] = None
    # Trace identifier (32 hex chars)
    trace_id: Optional[str] = None
    # W3C trace state
    trace_state: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> Span:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: Span
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return Span()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from ...common.attribute import Attribute
        from ...common.instrumentation_scope import InstrumentationScope
        from ..resource.resource import Resource
        from .span_event import SpanEvent
        from .span_link import SpanLink
        from .span_status import SpanStatus

        from ...common.attribute import Attribute
        from ...common.instrumentation_scope import InstrumentationScope
        from ..resource.resource import Resource
        from .span_event import SpanEvent
        from .span_link import SpanLink
        from .span_status import SpanStatus

        fields: dict[str, Callable[[Any], None]] = {
            "attributes": lambda n : setattr(self, 'attributes', n.get_collection_of_object_values(Attribute)),
            "dropped_attributes_count": lambda n : setattr(self, 'dropped_attributes_count', n.get_int_value()),
            "dropped_events_count": lambda n : setattr(self, 'dropped_events_count', n.get_int_value()),
            "dropped_links_count": lambda n : setattr(self, 'dropped_links_count', n.get_int_value()),
            "end_time_unix_nano": lambda n : setattr(self, 'end_time_unix_nano', n.get_int_value()),
            "events": lambda n : setattr(self, 'events', n.get_collection_of_object_values(SpanEvent)),
            "flags": lambda n : setattr(self, 'flags', n.get_int_value()),
            "instrumentation_scope": lambda n : setattr(self, 'instrumentation_scope', n.get_object_value(InstrumentationScope)),
            "kind": lambda n : setattr(self, 'kind', n.get_float_value()),
            "links": lambda n : setattr(self, 'links', n.get_collection_of_object_values(SpanLink)),
            "name": lambda n : setattr(self, 'name', n.get_str_value()),
            "parent_span_id": lambda n : setattr(self, 'parent_span_id', n.get_str_value()),
            "resource": lambda n : setattr(self, 'resource', n.get_object_value(Resource)),
            "span_id": lambda n : setattr(self, 'span_id', n.get_str_value()),
            "start_time_unix_nano": lambda n : setattr(self, 'start_time_unix_nano', n.get_int_value()),
            "status": lambda n : setattr(self, 'status', n.get_object_value(SpanStatus)),
            "trace_id": lambda n : setattr(self, 'trace_id', n.get_str_value()),
            "trace_state": lambda n : setattr(self, 'trace_state', n.get_str_value()),
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
        writer.write_collection_of_object_values("attributes", self.attributes)
        writer.write_int_value("dropped_attributes_count", self.dropped_attributes_count)
        writer.write_int_value("dropped_events_count", self.dropped_events_count)
        writer.write_int_value("dropped_links_count", self.dropped_links_count)
        writer.write_int_value("end_time_unix_nano", self.end_time_unix_nano)
        writer.write_collection_of_object_values("events", self.events)
        writer.write_int_value("flags", self.flags)
        writer.write_object_value("instrumentation_scope", self.instrumentation_scope)
        writer.write_float_value("kind", self.kind)
        writer.write_collection_of_object_values("links", self.links)
        writer.write_str_value("name", self.name)
        writer.write_str_value("parent_span_id", self.parent_span_id)
        writer.write_object_value("resource", self.resource)
        writer.write_str_value("span_id", self.span_id)
        writer.write_int_value("start_time_unix_nano", self.start_time_unix_nano)
        writer.write_object_value("status", self.status)
        writer.write_str_value("trace_id", self.trace_id)
        writer.write_str_value("trace_state", self.trace_state)
        writer.write_additional_data_value(self.additional_data)
    

