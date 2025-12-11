from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from ....common.attribute import Attribute
    from .correlated_error import CorrelatedError

@dataclass
class ErrorCorrelation(AdditionalDataHolder, Parsable):
    """
    Error correlation result
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Common attributes
    common_attributes: Optional[list[Attribute]] = None
    # Correlated errors
    correlated_errors: Optional[list[CorrelatedError]] = None
    # Error ID
    error_id: Optional[str] = None
    # Potential root cause
    root_cause: Optional[str] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> ErrorCorrelation:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: ErrorCorrelation
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return ErrorCorrelation()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from ....common.attribute import Attribute
        from .correlated_error import CorrelatedError

        from ....common.attribute import Attribute
        from .correlated_error import CorrelatedError

        fields: dict[str, Callable[[Any], None]] = {
            "common_attributes": lambda n : setattr(self, 'common_attributes', n.get_collection_of_object_values(Attribute)),
            "correlated_errors": lambda n : setattr(self, 'correlated_errors', n.get_collection_of_object_values(CorrelatedError)),
            "error_id": lambda n : setattr(self, 'error_id', n.get_str_value()),
            "root_cause": lambda n : setattr(self, 'root_cause', n.get_str_value()),
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
        writer.write_collection_of_object_values("common_attributes", self.common_attributes)
        writer.write_collection_of_object_values("correlated_errors", self.correlated_errors)
        writer.write_str_value("error_id", self.error_id)
        writer.write_str_value("root_cause", self.root_cause)
        writer.write_additional_data_value(self.additional_data)
    

