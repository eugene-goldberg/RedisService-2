using Microsoft.Extensions.DependencyInjection;
using Redis.OM;
using Redis.OM.Contracts;
using Redis.OM.Modeling;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

[Document(StorageType = StorageType.Json)]
public class Customer
{
    [RedisIdField]
    [Indexed]
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public Address Address { get; set; }
    public string Phone { get; set; }
    public string RegisteredDate { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string PostalCode { get; set; }
}

[Document(StorageType = StorageType.Json)]
public class Product
{
    [RedisIdField]
    [Indexed]
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
    public int StockQuantity { get; set; }
}

[Document(StorageType = StorageType.Json)]
public class Order
{
    [RedisIdField]
    [Indexed]
    public string Id { get; set; }
    [RedisIdField]
    [Indexed]
    public string CustomerId { get; set; }
    public List<OrderProduct> Products { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; }
}

public class OrderProduct
{
    public string ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}