

namespace QuantConnect.Orders;


public class OneInchWebsocketReceipt {
    public string message_code {get; set;}
    public int status {get; set;}
    public string transaction_hash {get; set;}
    public long gasUsed {get; set;}
    public string token_symbol {get; set;}
    public string order_id {get; set;}
    public decimal quote_amount {get; set;}
    public decimal commission {get; set;} 
    public decimal price {get; set;}
}

public class OneInchOrder {
    public string instruction_type {get; set;}
    public string quote_token {get; set;}
    public decimal quote_amount {get; set;}
    public decimal slip_rate {get; set;}
    public string wallet_address {get; set;}
    public string quote_token_address {get; set;}
    public string quote_token_symbol {get; set;}
    public int quote_token_decimal {get; set;}
    public string quote_token_name {get; set;}
    public string base_currency {get; set;}
    public int chain_id {get; set;}
    public string order_id {get;set;}
    public decimal estimate_usd {get;set;}

}