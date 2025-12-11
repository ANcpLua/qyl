from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from ...models.service_info import ServiceInfo

@dataclass
class ServicesGetResponse(AdditionalDataHolder, Parsable):
    """
    Cursor-based paginated response wrapper
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Whether there are more items available
    has_more: Optional[bool] = None
    # List of items in this page
    items: Optional[list[ServiceInfo]] = None
    # Cursor for the next page (null if no more pages)
    next_cursor: Optional[str] = None
    # Cursor for the previous page (null if first page)
    prev_cursor: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ServicesGetResponse:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ServicesGetResponse
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ServicesGetResponse()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from ...models.service_info import ServiceInfo

        from ...models.service_info import ServiceInfo

        fields: dict[str, Callable[[Any], None]] = {
            "has_more": lambda n : setattr(self, 'has_more', n.get_bool_value()),
            "items": lambda n : setattr(self, 'items', n.get_collection_of_object_values(ServiceInfo)),
            "next_cursor": lambda n : setattr(self, 'next_cursor', n.get_str_value()),
            "prev_cursor": lambda n : setattr(self, 'prev_cursor', n.get_str_value()),
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
        writer.write_bool_value("has_more", self.has_more)
        writer.write_collection_of_object_values("items", self.items)
        writer.write_str_value("next_cursor", self.next_cursor)
        writer.write_str_value("prev_cursor", self.prev_cursor)
        writer.write_additional_data_value(self.additional_data)
    

