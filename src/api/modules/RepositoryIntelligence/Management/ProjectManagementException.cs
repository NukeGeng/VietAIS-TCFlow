using System.Net;
using FSH.Framework.Core.Exceptions;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed class ProjectManagementValidationException(string message)
    : FshException(message, [], HttpStatusCode.BadRequest);
