from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.base_request_builder import BaseRequestBuilder
from kiota_abstractions.base_request_configuration import RequestConfiguration
from kiota_abstractions.default_query_parameters import QueryParameters
from kiota_abstractions.get_path_parameters import get_path_parameters
from kiota_abstractions.method import Method
from kiota_abstractions.request_adapter import RequestAdapter
from kiota_abstractions.request_information import RequestInformation
from kiota_abstractions.request_option import RequestOption
from kiota_abstractions.serialization import Parsable, ParsableFactory
from typing import Any, Optional, TYPE_CHECKING, Union
from warnings import warn

if TYPE_CHECKING:
    from ...models.qyl.common.errors.internal_server_error import InternalServerError
    from ...models.qyl.common.errors.validation_error import ValidationError
    from ...models.qyl.domains.observe.exceptions.exception_status import ExceptionStatus
    from .exceptions_get_response import ExceptionsGetResponse
    from .stats.stats_request_builder import StatsRequestBuilder

class ExceptionsRequestBuilder(BaseRequestBuilder):
    """
    Builds and executes requests for operations under /v1/exceptions
    """
    def __init__(self,request_adapter: RequestAdapter, path_parameters: Union[str, dict[str, Any]]) -> None:
        """
        Instantiates a new ExceptionsRequestBuilder and sets the default values.
        param path_parameters: The raw url or the url-template parameters for the request.
        param request_adapter: The request adapter to use to execute the requests.
        Returns: None
        """
        super().__init__(request_adapter, "{+baseurl}/v1/exceptions{?cursor,endTime,exceptionType,limit,serviceName,startTime,status}", path_parameters)
    
    async def get(self,request_configuration: Optional[RequestConfiguration[ExceptionsRequestBuilderGetQueryParameters]] = None) -> Optional[ExceptionsGetResponse]:
        """
        List exceptions
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: Optional[ExceptionsGetResponse]
        """
        request_info = self.to_get_request_information(
            request_configuration
        )
        from ...models.qyl.common.errors.internal_server_error import InternalServerError
        from ...models.qyl.common.errors.validation_error import ValidationError

        error_mapping: dict[str, type[ParsableFactory]] = {
            "400": ValidationError,
            "500": InternalServerError,
        }
        if not self.request_adapter:
            raise Exception("Http core is null") 
        from .exceptions_get_response import ExceptionsGetResponse

        return await self.request_adapter.send_async(request_info, ExceptionsGetResponse, error_mapping)
    
    def to_get_request_information(self,request_configuration: Optional[RequestConfiguration[ExceptionsRequestBuilderGetQueryParameters]] = None) -> RequestInformation:
        """
        List exceptions
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: RequestInformation
        """
        request_info = RequestInformation(Method.GET, self.url_template, self.path_parameters)
        request_info.configure(request_configuration)
        request_info.headers.try_add("Accept", "application/json")
        return request_info
    
    def with_url(self,raw_url: str) -> ExceptionsRequestBuilder:
        """
        Returns a request builder with the provided arbitrary URL. Using this method means any other path or query parameters are ignored.
        param raw_url: The raw URL to use for the request builder.
        Returns: ExceptionsRequestBuilder
        """
        if raw_url is None:
            raise TypeError("raw_url cannot be null.")
        return ExceptionsRequestBuilder(self.request_adapter, raw_url)
    
    @property
    def stats(self) -> StatsRequestBuilder:
        """
        The stats property
        """
        from .stats.stats_request_builder import StatsRequestBuilder

        return StatsRequestBuilder(self.request_adapter, self.path_parameters)
    
    @dataclass
    class ExceptionsRequestBuilderGetQueryParameters():
        """
        List exceptions
        """
        def get_query_parameter(self,original_name: str) -> str:
            """
            Maps the query parameters names to their encoded names for the URI template parsing.
            param original_name: The original query parameter name in the class.
            Returns: str
            """
            if original_name is None:
                raise TypeError("original_name cannot be null.")
            if original_name == "end_time":
                return "endTime"
            if original_name == "exception_type":
                return "exceptionType"
            if original_name == "service_name":
                return "serviceName"
            if original_name == "start_time":
                return "startTime"
            if original_name == "cursor":
                return "cursor"
            if original_name == "limit":
                return "limit"
            if original_name == "status":
                return "status"
            return original_name
        
        # Cursor
        cursor: Optional[str] = None

        # End time
        end_time: Optional[datetime.datetime] = None

        # Exception type filter
        exception_type: Optional[str] = None

        # Page size
        limit: Optional[int] = None

        # Service name filter
        service_name: Optional[str] = None

        # Start time
        start_time: Optional[datetime.datetime] = None

        # Status filter
        status: Optional[ExceptionStatus] = None

    
    @dataclass
    class ExceptionsRequestBuilderGetRequestConfiguration(RequestConfiguration[ExceptionsRequestBuilderGetQueryParameters]):
        """
        Configuration for the request such as headers, query parameters, and middleware options.
        """
        warn("This class is deprecated. Please use the generic RequestConfiguration class generated by the generator.", DeprecationWarning)
    

