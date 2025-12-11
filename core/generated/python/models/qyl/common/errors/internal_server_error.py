from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.api_error import APIError
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .problem_details import ProblemDetails

@dataclass
class InternalServerError(APIError, AdditionalDataHolder, Parsable):
    """
    Internal server error (500)
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # A URI reference identifying the problem type
    type: Optional[str] = "about:blank"
    # A human-readable explanation specific to this occurrence
    detail: Optional[str] = None
    # Error code for support reference
    error_code: Optional[str] = None
    # A URI reference identifying the specific occurrence
    instance: Optional[str] = None
    # The HTTP status code (informational only, actual code set by subtype)
    status: Optional[int] = None
    # Timestamp of the error
    timestamp: Optional[datetime.datetime] = None
    # The title property
    title: Optional[InternalServerError_title] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> InternalServerError:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: InternalServerError
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return InternalServerError()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .problem_details import ProblemDetails

        from .problem_details import ProblemDetails

        fields: dict[str, Callable[[Any], None]] = {
            "detail": lambda n : setattr(self, 'detail', n.get_str_value()),
            "error_code": lambda n : setattr(self, 'error_code', n.get_str_value()),
            "instance": lambda n : setattr(self, 'instance', n.get_str_value()),
            "status": lambda n : setattr(self, 'status', n.get_int_value()),
            "timestamp": lambda n : setattr(self, 'timestamp', n.get_datetime_value()),
            "type": lambda n : setattr(self, 'type', n.get_str_value()),
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
        writer.write_str_value("detail", self.detail)
        writer.write_str_value("error_code", self.error_code)
        writer.write_str_value("instance", self.instance)
        writer.write_int_value("status", self.status)
        writer.write_datetime_value("timestamp", self.timestamp)
        writer.write_str_value("type", self.type)
        writer.write_additional_data_value(self.additional_data)
    
    @property
    def primary_message(self) -> Optional[str]:
        """
        The primary error message.
        """
        return super().message

