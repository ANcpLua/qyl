from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .trace_query_tags import TraceQuery_tags

@dataclass
class TraceQuery(AdditionalDataHolder, Parsable):
    """
    Trace search query
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Cursor
    cursor: Optional[str] = None
    # Time range end
    end_time: Optional[datetime.datetime] = None
    # Page size
    limit: Optional[int] = None
    # Maximum duration in milliseconds
    max_duration_ms: Optional[int] = None
    # Minimum duration in milliseconds
    min_duration_ms: Optional[int] = None
    # Operation name filter
    operation_name: Optional[str] = None
    # Free text search
    query: Optional[str] = None
    # Service name filter
    service_name: Optional[str] = None
    # Time range start
    start_time: Optional[datetime.datetime] = None
    # Status filter
    status: Optional[float] = None
    # Tag filters
    tags: Optional[TraceQuery_tags] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> TraceQuery:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: TraceQuery
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return TraceQuery()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .trace_query_tags import TraceQuery_tags

        from .trace_query_tags import TraceQuery_tags

        fields: dict[str, Callable[[Any], None]] = {
            "cursor": lambda n : setattr(self, 'cursor', n.get_str_value()),
            "end_time": lambda n : setattr(self, 'end_time', n.get_datetime_value()),
            "limit": lambda n : setattr(self, 'limit', n.get_int_value()),
            "max_duration_ms": lambda n : setattr(self, 'max_duration_ms', n.get_int_value()),
            "min_duration_ms": lambda n : setattr(self, 'min_duration_ms', n.get_int_value()),
            "operation_name": lambda n : setattr(self, 'operation_name', n.get_str_value()),
            "query": lambda n : setattr(self, 'query', n.get_str_value()),
            "service_name": lambda n : setattr(self, 'service_name', n.get_str_value()),
            "start_time": lambda n : setattr(self, 'start_time', n.get_datetime_value()),
            "status": lambda n : setattr(self, 'status', n.get_float_value()),
            "tags": lambda n : setattr(self, 'tags', n.get_object_value(TraceQuery_tags)),
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
        writer.write_str_value("cursor", self.cursor)
        writer.write_datetime_value("end_time", self.end_time)
        writer.write_int_value("limit", self.limit)
        writer.write_int_value("max_duration_ms", self.max_duration_ms)
        writer.write_int_value("min_duration_ms", self.min_duration_ms)
        writer.write_str_value("operation_name", self.operation_name)
        writer.write_str_value("query", self.query)
        writer.write_str_value("service_name", self.service_name)
        writer.write_datetime_value("start_time", self.start_time)
        writer.write_float_value("status", self.status)
        writer.write_object_value("tags", self.tags)
        writer.write_additional_data_value(self.additional_data)
    

