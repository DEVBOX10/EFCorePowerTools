﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PostgresTester.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace PostgresTester.Models
{
    public partial interface INorthwindContextFunctions
    {
        Task<List<CustOrderHistResult>> CustOrderHistAsync(string CustomerID, CancellationToken cancellationToken = default);
        Task<List<EmployeeSalesbyCountryResult>> EmployeeSalesbyCountryAsync(DateTime? Beginning_Date, DateTime? Ending_Date, CancellationToken cancellationToken = default);
        Task<List<SalesbyYearResult>> SalesbyYearAsync(DateTime? Beginning_Date, DateTime? Ending_Date, CancellationToken cancellationToken = default);
        Task<int> sum_xyAsync(int? _x, int? _y, OutputParameter<int?> _sum, CancellationToken cancellationToken = default);
    }
}
