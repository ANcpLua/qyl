from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .device_type import DeviceType

@dataclass
class SessionClientInfo(AdditionalDataHolder, Parsable):
    """
    Session client information
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Browser name
    browser: Optional[str] = None
    # Browser version
    browser_version: Optional[str] = None
    # Device type
    device_type: Optional[DeviceType] = None
    # Client IP address
    ip: Optional[str] = None
    # Operating system
    os: Optional[str] = None
    # User agent string
    user_agent: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> SessionClientInfo:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: SessionClientInfo
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return SessionClientInfo()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .device_type import DeviceType

        from .device_type import DeviceType

        fields: dict[str, Callable[[Any], None]] = {
            "browser": lambda n : setattr(self, 'browser', n.get_str_value()),
            "browser_version": lambda n : setattr(self, 'browser_version', n.get_str_value()),
            "device_type": lambda n : setattr(self, 'device_type', n.get_enum_value(DeviceType)),
            "ip": lambda n : setattr(self, 'ip', n.get_str_value()),
            "os": lambda n : setattr(self, 'os', n.get_str_value()),
            "user_agent": lambda n : setattr(self, 'user_agent', n.get_str_value()),
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
        writer.write_str_value("browser", self.browser)
        writer.write_str_value("browser_version", self.browser_version)
        writer.write_enum_value("device_type", self.device_type)
        writer.write_str_value("ip", self.ip)
        writer.write_str_value("os", self.os)
        writer.write_str_value("user_agent", self.user_agent)
        writer.write_additional_data_value(self.additional_data)
    

