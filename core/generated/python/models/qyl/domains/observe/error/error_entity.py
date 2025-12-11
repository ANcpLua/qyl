from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .error_category import ErrorCategory
    from .error_status import ErrorStatus

@dataclass
class ErrorEntity(AdditionalDataHolder, Parsable):
    """
    Error entity for tracking and analysis
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Affected services
    affected_services: Optional[list[str]] = None
    # Affected users count
    affected_users: Optional[int] = None
    # Assigned to
    assigned_to: Optional[str] = None
    # Error category
    category: Optional[ErrorCategory] = None
    # Error ID
    error_id: Optional[str] = None
    # Error type (class name or code)
    error_type: Optional[str] = None
    # Fingerprint for grouping
    fingerprint: Optional[str] = None
    # First occurrence
    first_seen: Optional[datetime.datetime] = None
    # Issue tracker URL
    issue_url: Optional[str] = None
    # Last occurrence
    last_seen: Optional[datetime.datetime] = None
    # Error message
    message: Optional[str] = None
    # Occurrence count
    occurrence_count: Optional[int] = None
    # Sample trace IDs
    sample_traces: Optional[list[str]] = None
    # Status
    status: Optional[ErrorStatus] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ErrorEntity:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ErrorEntity
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ErrorEntity()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .error_category import ErrorCategory
        from .error_status import ErrorStatus

        from .error_category import ErrorCategory
        from .error_status import ErrorStatus

        fields: dict[str, Callable[[Any], None]] = {
            "affected_services": lambda n : setattr(self, 'affected_services', n.get_collection_of_primitive_values(str)),
            "affected_users": lambda n : setattr(self, 'affected_users', n.get_int_value()),
            "assigned_to": lambda n : setattr(self, 'assigned_to', n.get_str_value()),
            "category": lambda n : setattr(self, 'category', n.get_enum_value(ErrorCategory)),
            "error_id": lambda n : setattr(self, 'error_id', n.get_str_value()),
            "error.type": lambda n : setattr(self, 'error_type', n.get_str_value()),
            "fingerprint": lambda n : setattr(self, 'fingerprint', n.get_str_value()),
            "first_seen": lambda n : setattr(self, 'first_seen', n.get_datetime_value()),
            "issue_url": lambda n : setattr(self, 'issue_url', n.get_str_value()),
            "last_seen": lambda n : setattr(self, 'last_seen', n.get_datetime_value()),
            "message": lambda n : setattr(self, 'message', n.get_str_value()),
            "occurrence_count": lambda n : setattr(self, 'occurrence_count', n.get_int_value()),
            "sample_traces": lambda n : setattr(self, 'sample_traces', n.get_collection_of_primitive_values(str)),
            "status": lambda n : setattr(self, 'status', n.get_enum_value(ErrorStatus)),
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
        writer.write_collection_of_primitive_values("affected_services", self.affected_services)
        writer.write_int_value("affected_users", self.affected_users)
        writer.write_str_value("assigned_to", self.assigned_to)
        writer.write_enum_value("category", self.category)
        writer.write_str_value("error_id", self.error_id)
        writer.write_str_value("error.type", self.error_type)
        writer.write_str_value("fingerprint", self.fingerprint)
        writer.write_datetime_value("first_seen", self.first_seen)
        writer.write_str_value("issue_url", self.issue_url)
        writer.write_datetime_value("last_seen", self.last_seen)
        writer.write_str_value("message", self.message)
        writer.write_int_value("occurrence_count", self.occurrence_count)
        writer.write_collection_of_primitive_values("sample_traces", self.sample_traces)
        writer.write_enum_value("status", self.status)
        writer.write_additional_data_value(self.additional_data)
    

