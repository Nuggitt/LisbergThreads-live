using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace LisbergThreads.Contracts.Customers;

public sealed record CustomerSummaryDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email
);
