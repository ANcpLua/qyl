from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .health_response_components import HealthResponse_components
    from .health_status import HealthStatus

@dataclass
class HealthResponse(AdditionalDataHolder, Parsable):
    """
    Health check response
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Component health
    components: Optional[HealthResponse_components] = None
    # Service status
    status: Optional[HealthStatus] = None
    # Uptime in seconds
    uptime_seconds: Optional[int] = None
    # Service version
    version: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> HealthResponse:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: HealthResponse
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return HealthResponse()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .health_response_components import HealthResponse_components
        from .health_status import HealthStatus

        from .health_response_components import HealthResponse_components
        from .health_status import HealthStatus

        fields: dict[str, Callable[[Any], None]] = {
            "components": lambda n : setattr(self, 'components', n.get_object_value(HealthResponse_components)),
            "status": lambda n : setattr(self, 'status', n.get_enum_value(HealthStatus)),
            "uptime_seconds": lambda n : setattr(self, 'uptime_seconds', n.get_int_value()),
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
        writer.write_object_value("components", self.components)
        writer.write_enum_value("status", self.status)
        writer.write_int_value("uptime_seconds", self.uptime_seconds)
        writer.write_str_value("version", self.version)
        writer.write_additional_data_value(self.additional_data)
    

