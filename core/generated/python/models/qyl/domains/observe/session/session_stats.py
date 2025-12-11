from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .session_country_stats import SessionCountryStats
    from .session_device_stats import SessionDeviceStats

@dataclass
class SessionStats(AdditionalDataHolder, Parsable):
    """
    Aggregated session statistics
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Active sessions count
    active_sessions: Optional[int] = None
    # Average session duration in milliseconds
    avg_duration_ms: Optional[float] = None
    # Bounce rate (single-page sessions)
    bounce_rate: Optional[float] = None
    # Sessions by country
    by_country: Optional[list[SessionCountryStats]] = None
    # Sessions by device type
    by_device_type: Optional[list[SessionDeviceStats]] = None
    # Sessions with errors
    sessions_with_errors: Optional[int] = None
    # Sessions with GenAI usage
    sessions_with_genai: Optional[int] = None
    # Total sessions in time range
    total_sessions: Optional[int] = None
    # Unique users in time range
    unique_users: Optional[int] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> SessionStats:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: SessionStats
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return SessionStats()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .session_country_stats import SessionCountryStats
        from .session_device_stats import SessionDeviceStats

        from .session_country_stats import SessionCountryStats
        from .session_device_stats import SessionDeviceStats

        fields: dict[str, Callable[[Any], None]] = {
            "active_sessions": lambda n : setattr(self, 'active_sessions', n.get_int_value()),
            "avg_duration_ms": lambda n : setattr(self, 'avg_duration_ms', n.get_float_value()),
            "bounce_rate": lambda n : setattr(self, 'bounce_rate', n.get_float_value()),
            "by_country": lambda n : setattr(self, 'by_country', n.get_collection_of_object_values(SessionCountryStats)),
            "by_device_type": lambda n : setattr(self, 'by_device_type', n.get_collection_of_object_values(SessionDeviceStats)),
            "sessions_with_errors": lambda n : setattr(self, 'sessions_with_errors', n.get_int_value()),
            "sessions_with_genai": lambda n : setattr(self, 'sessions_with_genai', n.get_int_value()),
            "total_sessions": lambda n : setattr(self, 'total_sessions', n.get_int_value()),
            "unique_users": lambda n : setattr(self, 'unique_users', n.get_int_value()),
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
        writer.write_int_value("active_sessions", self.active_sessions)
        writer.write_float_value("avg_duration_ms", self.avg_duration_ms)
        writer.write_float_value("bounce_rate", self.bounce_rate)
        writer.write_collection_of_object_values("by_country", self.by_country)
        writer.write_collection_of_object_values("by_device_type", self.by_device_type)
        writer.write_int_value("sessions_with_errors", self.sessions_with_errors)
        writer.write_int_value("sessions_with_genai", self.sessions_with_genai)
        writer.write_int_value("total_sessions", self.total_sessions)
        writer.write_int_value("unique_users", self.unique_users)
        writer.write_additional_data_value(self.additional_data)
    

