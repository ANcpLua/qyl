from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .session_client_info import SessionClientInfo
    from .session_gen_ai_usage import SessionGenAiUsage
    from .session_geo_info import SessionGeoInfo
    from .session_state import SessionState

@dataclass
class SessionEntity(AdditionalDataHolder, Parsable):
    """
    Complete session entity with aggregated data
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Client information
    client: Optional[SessionClientInfo] = None
    # Session duration in milliseconds
    duration_ms: Optional[float] = None
    # Session end time
    end_time: Optional[datetime.datetime] = None
    # Total error count in session
    error_count: Optional[int] = None
    # GenAI usage summary
    genai_usage: Optional[SessionGenAiUsage] = None
    # Location information
    geo: Optional[SessionGeoInfo] = None
    # Session ID
    session_id: Optional[str] = None
    # Total span count in session
    span_count: Optional[int] = None
    # Session start time
    start_time: Optional[datetime.datetime] = None
    # Session state
    state: Optional[SessionState] = None
    # Total trace count in session
    trace_count: Optional[int] = None
    # User ID (if authenticated)
    user_id: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> SessionEntity:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: SessionEntity
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return SessionEntity()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .session_client_info import SessionClientInfo
        from .session_gen_ai_usage import SessionGenAiUsage
        from .session_geo_info import SessionGeoInfo
        from .session_state import SessionState

        from .session_client_info import SessionClientInfo
        from .session_gen_ai_usage import SessionGenAiUsage
        from .session_geo_info import SessionGeoInfo
        from .session_state import SessionState

        fields: dict[str, Callable[[Any], None]] = {
            "client": lambda n : setattr(self, 'client', n.get_object_value(SessionClientInfo)),
            "duration_ms": lambda n : setattr(self, 'duration_ms', n.get_float_value()),
            "end_time": lambda n : setattr(self, 'end_time', n.get_datetime_value()),
            "error_count": lambda n : setattr(self, 'error_count', n.get_int_value()),
            "genai_usage": lambda n : setattr(self, 'genai_usage', n.get_object_value(SessionGenAiUsage)),
            "geo": lambda n : setattr(self, 'geo', n.get_object_value(SessionGeoInfo)),
            "session.id": lambda n : setattr(self, 'session_id', n.get_str_value()),
            "span_count": lambda n : setattr(self, 'span_count', n.get_int_value()),
            "start_time": lambda n : setattr(self, 'start_time', n.get_datetime_value()),
            "state": lambda n : setattr(self, 'state', n.get_enum_value(SessionState)),
            "trace_count": lambda n : setattr(self, 'trace_count', n.get_int_value()),
            "user.id": lambda n : setattr(self, 'user_id', n.get_str_value()),
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
        writer.write_object_value("client", self.client)
        writer.write_float_value("duration_ms", self.duration_ms)
        writer.write_datetime_value("end_time", self.end_time)
        writer.write_int_value("error_count", self.error_count)
        writer.write_object_value("genai_usage", self.genai_usage)
        writer.write_object_value("geo", self.geo)
        writer.write_str_value("session.id", self.session_id)
        writer.write_int_value("span_count", self.span_count)
        writer.write_datetime_value("start_time", self.start_time)
        writer.write_enum_value("state", self.state)
        writer.write_int_value("trace_count", self.trace_count)
        writer.write_str_value("user.id", self.user_id)
        writer.write_additional_data_value(self.additional_data)
    

