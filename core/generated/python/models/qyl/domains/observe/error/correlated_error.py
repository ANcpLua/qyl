from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .temporal_relationship import TemporalRelationship

@dataclass
class CorrelatedError(AdditionalDataHolder, Parsable):
    """
    Correlated error
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Correlation strength
    correlation_strength: Optional[float] = None
    # Error ID
    error_id: Optional[str] = None
    # Error type
    error_type: Optional[str] = None
    # Temporal relationship
    temporal_relationship: Optional[TemporalRelationship] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> CorrelatedError:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: CorrelatedError
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return CorrelatedError()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .temporal_relationship import TemporalRelationship

        from .temporal_relationship import TemporalRelationship

        fields: dict[str, Callable[[Any], None]] = {
            "correlation_strength": lambda n : setattr(self, 'correlation_strength', n.get_float_value()),
            "error_id": lambda n : setattr(self, 'error_id', n.get_str_value()),
            "error_type": lambda n : setattr(self, 'error_type', n.get_str_value()),
            "temporal_relationship": lambda n : setattr(self, 'temporal_relationship', n.get_enum_value(TemporalRelationship)),
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
        writer.write_float_value("correlation_strength", self.correlation_strength)
        writer.write_str_value("error_id", self.error_id)
        writer.write_str_value("error_type", self.error_type)
        writer.write_enum_value("temporal_relationship", self.temporal_relationship)
        writer.write_additional_data_value(self.additional_data)
    

