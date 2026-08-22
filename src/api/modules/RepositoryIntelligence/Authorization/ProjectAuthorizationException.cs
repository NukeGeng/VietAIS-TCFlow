using System.Net;
using FSH.Framework.Core.Exceptions;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

public sealed class ProjectAuthorizationValidationException(string message)
    : FshException(message, [], HttpStatusCode.BadRequest);
