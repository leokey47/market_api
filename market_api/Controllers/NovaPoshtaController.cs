using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace market_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NovaPoshtaController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NovaPoshtaController> _logger;
        private readonly string _apiKey;
        private readonly string _apiUrl = "https://api.novaposhta.ua/v2.0/json/";

        public NovaPoshtaController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<NovaPoshtaController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _apiKey = _configuration["NovaPoshta:ApiKey"];
        }

        // GET: api/NovaPoshta/cities
        [HttpGet("cities")]
        public async Task<IActionResult> GetCities(string search = "")
        {
            try
            {
                var request = new
                {
                    apiKey = _apiKey,
                    modelName = "Address",
                    calledMethod = "getCities",
                    methodProperties = new
                    {
                        FindByString = search,
                        Limit = 20
                    }
                };

                var response = await SendNovaPoshtaRequest(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cities from Nova Poshta");
                return StatusCode(500, "Error retrieving cities from Nova Poshta");
            }
        }

        // GET: api/NovaPoshta/warehouses/{cityRef}
        [HttpGet("warehouses/{cityRef}")]
        public async Task<IActionResult> GetWarehouses(string cityRef)
        {
            try
            {
                var request = new
                {
                    apiKey = _apiKey,
                    modelName = "AddressGeneral",
                    calledMethod = "getWarehouses",
                    methodProperties = new
                    {
                        CityRef = cityRef,
                        Language = "UA"
                    }
                };

                var response = await SendNovaPoshtaRequest(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting warehouses from Nova Poshta");
                return StatusCode(500, "Error retrieving warehouses from Nova Poshta");
            }
        }

        // GET: api/NovaPoshta/areas
        [HttpGet("areas")]
        public async Task<IActionResult> GetAreas()
        {
            try
            {
                var request = new
                {
                    apiKey = _apiKey,
                    modelName = "Address",
                    calledMethod = "getAreas",
                    methodProperties = new { }
                };

                var response = await SendNovaPoshtaRequest(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting areas from Nova Poshta");
                return StatusCode(500, "Error retrieving areas from Nova Poshta");
            }
        }

        // GET: api/NovaPoshta/settlement-types
        [HttpGet("settlement-types")]
        public async Task<IActionResult> GetSettlementTypes()
        {
            try
            {
                var request = new
                {
                    apiKey = _apiKey,
                    modelName = "Address",
                    calledMethod = "getSettlementTypes",
                    methodProperties = new { }
                };

                var response = await SendNovaPoshtaRequest(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting settlement types from Nova Poshta");
                return StatusCode(500, "Error retrieving settlement types from Nova Poshta");
            }
        }

        // POST: api/NovaPoshta/calculate
        [HttpPost("calculate")]
        public async Task<IActionResult> CalculateDelivery([FromBody] DeliveryCalculationRequest calculationRequest)
        {
            try
            {
                var request = new
                {
                    apiKey = _apiKey,
                    modelName = "InternetDocument",
                    calledMethod = "getDocumentPrice",
                    methodProperties = new
                    {
                        CitySender = calculationRequest.SenderCityRef,
                        CityRecipient = calculationRequest.RecipientCityRef,
                        Weight = calculationRequest.Weight,
                        ServiceType = calculationRequest.ServiceType ?? "WarehouseWarehouse",
                        Cost = calculationRequest.DeclaredValue,
                        CargoType = calculationRequest.CargoType ?? "Cargo",
                        SeatsAmount = calculationRequest.SeatsAmount ?? 1
                    }
                };

                var response = await SendNovaPoshtaRequest(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating delivery cost with Nova Poshta");
                return StatusCode(500, "Error calculating delivery cost");
            }
        }

        // POST: api/NovaPoshta/create-shipping
        [HttpPost("create-shipping")]
        [Authorize]
        public async Task<IActionResult> CreateShipping([FromBody] CreateShippingRequest shippingRequest)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
                if (userId == 0)
                    return Unauthorized();

                // Формируем запрос для создания экспресс-накладной
                var request = new
                {
                    apiKey = _apiKey,
                    modelName = "InternetDocument",
                    calledMethod = "save",
                    methodProperties = new
                    {
                        SenderWarehouseIndex = shippingRequest.SenderWarehouseIndex,
                        RecipientWarehouseIndex = shippingRequest.RecipientWarehouseIndex,
                        PayerType = shippingRequest.PayerType,
                        PaymentMethod = shippingRequest.PaymentMethod,
                        CargoType = shippingRequest.CargoType,
                        VolumeGeneral = shippingRequest.VolumeGeneral,
                        Weight = shippingRequest.Weight,
                        ServiceType = shippingRequest.ServiceType,
                        SeatsAmount = shippingRequest.SeatsAmount,
                        Description = shippingRequest.Description,
                        Cost = shippingRequest.Cost,
                        CitySender = shippingRequest.CitySender,
                        Sender = shippingRequest.Sender,
                        SenderAddress = shippingRequest.SenderAddress,
                        ContactSender = shippingRequest.ContactSender,
                        SenderId = shippingRequest.SenderId,
                        CityRecipient = shippingRequest.CityRecipient,
                        Recipient = shippingRequest.Recipient,
                        RecipientAddress = shippingRequest.RecipientAddress,
                        ContactRecipient = shippingRequest.ContactRecipient,
                        RecipientId = shippingRequest.RecipientId
                    }
                };

                var response = await SendNovaPoshtaRequest(request);

                // Тут можно добавить сохранение информации о созданной накладной в БД
                // Например, связать с заказом пользователя

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating shipping with Nova Poshta");
                return StatusCode(500, "Error creating shipping");
            }
        }

        private async Task<object> SendNovaPoshtaRequest(object requestData)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(_apiUrl, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<object>(responseContent);
        }
    }

    // DTO для запросов
    public class DeliveryCalculationRequest
    {
        public string SenderCityRef { get; set; }
        public string RecipientCityRef { get; set; }
        public double Weight { get; set; }
        public string ServiceType { get; set; }
        public decimal DeclaredValue { get; set; }
        public string CargoType { get; set; }
        public int? SeatsAmount { get; set; }
    }

    public class CreateShippingRequest
    {
        // Основные данные об отправлении
        public string PayerType { get; set; } // Кто платит за доставку
        public string PaymentMethod { get; set; } // Способ оплаты
        public string CargoType { get; set; } // Тип груза
        public double VolumeGeneral { get; set; } // Общий объем
        public double Weight { get; set; } // Вес
        public string ServiceType { get; set; } // Тип услуги
        public int SeatsAmount { get; set; } // Количество мест
        public string Description { get; set; } // Описание
        public decimal Cost { get; set; } // Объявленная стоимость

        // Данные отправителя
        public string CitySender { get; set; } // Реф города отправителя
        public string Sender { get; set; } // Реф отправителя
        public string SenderAddress { get; set; } // Реф адреса отправителя
        public string ContactSender { get; set; } // Реф контакта отправителя
        public string SenderId { get; set; } // ID отправителя
        public string SenderWarehouseIndex { get; set; } // Индекс отделения отправителя

        // Данные получателя
        public string CityRecipient { get; set; } // Реф города получателя
        public string Recipient { get; set; } // Реф получателя
        public string RecipientAddress { get; set; } // Реф адреса получателя
        public string ContactRecipient { get; set; } // Реф контакта получателя
        public string RecipientId { get; set; } // ID получателя
        public string RecipientWarehouseIndex { get; set; } // Индекс отделения получателя
    }
}