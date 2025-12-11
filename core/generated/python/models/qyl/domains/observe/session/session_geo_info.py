from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

@dataclass
class SessionGeoInfo(AdditionalDataHolder, Parsable):
    """
    Session geographic information
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # City
    city: Optional[str] = None
    # Country code (ISO 3166-1 alpha-2)
    country_code: Optional[str] = None
    # Country name
    country_name: Optional[str] = None
    # Postal code
    postal_code: Optional[str] = None
    # Region/state
    region: Optional[str] = None
    # Timezone
    timezone: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> SessionGeoInfo:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: SessionGeoInfo
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return SessionGeoInfo()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        fields: dict[str, Callable[[Any], None]] = {
            "city": lambda n : setattr(self, 'city', n.get_str_value()),
            "country_code": lambda n : setattr(self, 'country_code', n.get_str_value()),
            "country_name": lambda n : setattr(self, 'country_name', n.get_str_value()),
            "postal_code": lambda n : setattr(self, 'postal_code', n.get_str_value()),
            "region": lambda n : setattr(self, 'region', n.get_str_value()),
            "timezone": lambda n : setattr(self, 'timezone', n.get_str_value()),
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
        writer.write_str_value("city", self.city)
        writer.write_str_value("country_code", self.country_code)
        writer.write_str_value("country_name", self.country_name)
        writer.write_str_value("postal_code", self.postal_code)
        writer.write_str_value("region", self.region)
        writer.write_str_value("timezone", self.timezone)
        writer.write_additional_data_value(self.additional_data)
    

