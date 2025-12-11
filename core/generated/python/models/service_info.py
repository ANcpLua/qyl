from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

@dataclass
class ServiceInfo(AdditionalDataHolder, Parsable):
    """
    Service information
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Instance count
    instance_count: Optional[int] = None
    # Last seen
    last_seen: Optional[datetime.datetime] = None
    # Service name
    name: Optional[str] = None
    # Service namespace
    namespace_name: Optional[str] = None
    # Service version
    version: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ServiceInfo:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ServiceInfo
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ServiceInfo()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        fields: dict[str, Callable[[Any], None]] = {
            "instance_count": lambda n : setattr(self, 'instance_count', n.get_int_value()),
            "last_seen": lambda n : setattr(self, 'last_seen', n.get_datetime_value()),
            "name": lambda n : setattr(self, 'name', n.get_str_value()),
            "namespace_name": lambda n : setattr(self, 'namespace_name', n.get_str_value()),
            "version": lambda n : setattr(self, 'version', n.get_str_value()),
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
        writer.write_int_value("instance_count", self.instance_count)
        writer.write_datetime_value("last_seen", self.last_seen)
        writer.write_str_value("name", self.name)
        writer.write_str_value("namespace_name", self.namespace_name)
        writer.write_str_value("version", self.version)
        writer.write_additional_data_value(self.additional_data)
    

