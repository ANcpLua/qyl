from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from ....common.attribute import Attribute
    from ...a_i.code.stack_trace import StackTrace
    from .exception_status import ExceptionStatus

@dataclass
class EnrichedException(AdditionalDataHolder, Parsable):
    """
    Enriched exception with parsed stack trace
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Affected users count
    affected_users: Optional[int] = None
    # Exception cause/inner exception
    cause: Optional[EnrichedException] = None
    # Additional exception data
    data: Optional[list[Attribute]] = None
    # Exception type/class name
    exception_type: Optional[str] = None
    # Exception fingerprint (for grouping)
    fingerprint: Optional[str] = None
    # First occurrence timestamp
    first_seen: Optional[datetime.datetime] = None
    # Last occurrence timestamp
    last_seen: Optional[datetime.datetime] = None
    # Exception message
    message: Optional[str] = None
    # Occurrence count
    occurrence_count: Optional[int] = None
    # Parsed stack trace
    stack_trace: Optional[StackTrace] = None
    # Status
    status: Optional[ExceptionStatus] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> EnrichedException:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: EnrichedException
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return EnrichedException()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from ....common.attribute import Attribute
        from ...a_i.code.stack_trace import StackTrace
        from .exception_status import ExceptionStatus

        from ....common.attribute import Attribute
        from ...a_i.code.stack_trace import StackTrace
        from .exception_status import ExceptionStatus

        fields: dict[str, Callable[[Any], None]] = {
            "affected_users": lambda n : setattr(self, 'affected_users', n.get_int_value()),
            "cause": lambda n : setattr(self, 'cause', n.get_object_value(EnrichedException)),
            "data": lambda n : setattr(self, 'data', n.get_collection_of_object_values(Attribute)),
            "exception_type": lambda n : setattr(self, 'exception_type', n.get_str_value()),
            "fingerprint": lambda n : setattr(self, 'fingerprint', n.get_str_value()),
            "first_seen": lambda n : setattr(self, 'first_seen', n.get_datetime_value()),
            "last_seen": lambda n : setattr(self, 'last_seen', n.get_datetime_value()),
            "message": lambda n : setattr(self, 'message', n.get_str_value()),
            "occurrence_count": lambda n : setattr(self, 'occurrence_count', n.get_int_value()),
            "stack_trace": lambda n : setattr(self, 'stack_trace', n.get_object_value(StackTrace)),
            "status": lambda n : setattr(self, 'status', n.get_enum_value(ExceptionStatus)),
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
        writer.write_int_value("affected_users", self.affected_users)
        writer.write_object_value("cause", self.cause)
        writer.write_collection_of_object_values("data", self.data)
        writer.write_str_value("exception_type", self.exception_type)
        writer.write_str_value("fingerprint", self.fingerprint)
        writer.write_datetime_value("first_seen", self.first_seen)
        writer.write_datetime_value("last_seen", self.last_seen)
        writer.write_str_value("message", self.message)
        writer.write_int_value("occurrence_count", self.occurrence_count)
        writer.write_object_value("stack_trace", self.stack_trace)
        writer.write_enum_value("status", self.status)
        writer.write_additional_data_value(self.additional_data)
    

