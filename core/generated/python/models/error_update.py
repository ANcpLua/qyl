from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .qyl.domains.observe.error.error_status import ErrorStatus

@dataclass
class ErrorUpdate(AdditionalDataHolder, Parsable):
    """
    Error update request
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Assignee
    assigned_to: Optional[str] = None
    # Issue URL
    issue_url: Optional[str] = None
    # New status
    status: Optional[ErrorStatus] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ErrorUpdate:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ErrorUpdate
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ErrorUpdate()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .qyl.domains.observe.error.error_status import ErrorStatus

        from .qyl.domains.observe.error.error_status import ErrorStatus

        fields: dict[str, Callable[[Any], None]] = {
            "assigned_to": lambda n : setattr(self, 'assigned_to', n.get_str_value()),
            "issue_url": lambda n : setattr(self, 'issue_url', n.get_str_value()),
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
        writer.write_str_value("assigned_to", self.assigned_to)
        writer.write_str_value("issue_url", self.issue_url)
        writer.write_enum_value("status", self.status)
        writer.write_additional_data_value(self.additional_data)
    

