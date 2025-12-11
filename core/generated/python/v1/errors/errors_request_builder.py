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
    from ...models.qyl.domains.observe.error.error_category import ErrorCategory
    from ...models.qyl.domains.observe.error.error_status import ErrorStatus
    from .errors_get_response import ErrorsGetResponse
    from .item.with_error_item_request_builder import WithErrorItemRequestBuilder
    from .stats.stats_request_builder import StatsRequestBuilder

class ErrorsRequestBuilder(BaseRequestBuilder):
    """
    Builds and executes requests for operations under /v1/errors
    """
    def __init__(self,request_adapter: RequestAdapter, path_parameters: Union[str, dict[str, Any]]) -> None:
        """
        Instantiates a new ErrorsRequestBuilder and sets the default values.
        param path_parameters: The raw url or the url-template parameters for the request.
        param request_adapter: The request adapter to use to execute the requests.
        Returns: None
        """
        super().__init__(request_adapter, "{+baseurl}/v1/errors{?category,cursor,endTime,limit,serviceName,startTime,status}", path_parameters)
    
    def by_error_id(self,error_id: str) -> WithErrorItemRequestBuilder:
        """
        Gets an item from the ApiSdk.v1.errors.item collection
        param error_id: Unique identifier of the item
        Returns: WithErrorItemRequestBuilder
        """
        if error_id is None:
            raise TypeError("error_id cannot be null.")
        from .item.with_error_item_request_builder import WithErrorItemRequestBuilder

        url_tpl_params = get_path_parameters(self.path_parameters)
        url_tpl_params["errorId"] = error_id
        return WithErrorItemRequestBuilder(self.request_adapter, url_tpl_params)
    
    async def get(self,request_configuration: Optional[RequestConfiguration[ErrorsRequestBuilderGetQueryParameters]] = None) -> Optional[ErrorsGetResponse]:
        """
        List error groups
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: Optional[ErrorsGetResponse]
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
        from .errors_get_response import ErrorsGetResponse

        return await self.request_adapter.send_async(request_info, ErrorsGetResponse, error_mapping)
    
    def to_get_request_information(self,request_configuration: Optional[RequestConfiguration[ErrorsRequestBuilderGetQueryParameters]] = None) -> RequestInformation:
        """
        List error groups
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: RequestInformation
        """
        request_info = RequestInformation(Method.GET, self.url_template, self.path_parameters)
        request_info.configure(request_configuration)
        request_info.headers.try_add("Accept", "application/json")
        return request_info
    
    def with_url(self,raw_url: str) -> ErrorsRequestBuilder:
        """
        Returns a request builder with the provided arbitrary URL. Using this method means any other path or query parameters are ignored.
        param raw_url: The raw URL to use for the request builder.
        Returns: ErrorsRequestBuilder
        """
        if raw_url is None:
            raise TypeError("raw_url cannot be null.")
        return ErrorsRequestBuilder(self.request_adapter, raw_url)
    
    @property
    def stats(self) -> StatsRequestBuilder:
        """
        The stats property
        """
        from .stats.stats_request_builder import StatsRequestBuilder

        return StatsRequestBuilder(self.request_adapter, self.path_parameters)
    
    @dataclass
    class ErrorsRequestBuilderGetQueryParameters():
        """
        List error groups
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
            if original_name == "service_name":
                return "serviceName"
            if original_name == "start_time":
                return "startTime"
            if original_name == "category":
                return "category"
            if original_name == "cursor":
                return "cursor"
            if original_name == "limit":
                return "limit"
            if original_name == "status":
                return "status"
            return original_name
        
        # Category filter
        category: Optional[ErrorCategory] = None

        # Cursor
        cursor: Optional[str] = None

        # End time
        end_time: Optional[datetime.datetime] = None

        # Page size
        limit: Optional[int] = None

        # Service name filter
        service_name: Optional[str] = None

        # Start time
        start_time: Optional[datetime.datetime] = None

        # Status filter
        status: Optional[ErrorStatus] = None

    
    @dataclass
    class ErrorsRequestBuilderGetRequestConfiguration(RequestConfiguration[ErrorsRequestBuilderGetQueryParameters]):
        """
        Configuration for the request such as headers, query parameters, and middleware options.
        """
        warn("This class is deprecated. Please use the generic RequestConfiguration class generated by the generator.", DeprecationWarning)
    

