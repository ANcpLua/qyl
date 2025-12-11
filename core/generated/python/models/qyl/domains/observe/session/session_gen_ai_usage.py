from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

@dataclass
class SessionGenAiUsage(AdditionalDataHolder, Parsable):
    """
    Session GenAI usage summary
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Estimated cost in USD
    estimated_cost_usd: Optional[float] = None
    # Models used in session
    models_used: Optional[list[str]] = None
    # Providers used in session
    providers_used: Optional[list[str]] = None
    # Total GenAI requests in session
    request_count: Optional[int] = None
    # Total input tokens consumed
    total_input_tokens: Optional[int] = None
    # Total output tokens generated
    total_output_tokens: Optional[int] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> SessionGenAiUsage:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: SessionGenAiUsage
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return SessionGenAiUsage()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        fields: dict[str, Callable[[Any], None]] = {
            "estimated_cost_usd": lambda n : setattr(self, 'estimated_cost_usd', n.get_float_value()),
            "models_used": lambda n : setattr(self, 'models_used', n.get_collection_of_primitive_values(str)),
            "providers_used": lambda n : setattr(self, 'providers_used', n.get_collection_of_primitive_values(str)),
            "request_count": lambda n : setattr(self, 'request_count', n.get_int_value()),
            "total_input_tokens": lambda n : setattr(self, 'total_input_tokens', n.get_int_value()),
            "total_output_tokens": lambda n : setattr(self, 'total_output_tokens', n.get_int_value()),
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
        writer.write_float_value("estimated_cost_usd", self.estimated_cost_usd)
        writer.write_collection_of_primitive_values("models_used", self.models_used)
        writer.write_collection_of_primitive_values("providers_used", self.providers_used)
        writer.write_int_value("request_count", self.request_count)
        writer.write_int_value("total_input_tokens", self.total_input_tokens)
        writer.write_int_value("total_output_tokens", self.total_output_tokens)
        writer.write_additional_data_value(self.additional_data)
    

