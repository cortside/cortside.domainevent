using Serilog;
using System;
using System.Threading.Tasks;

namespace Spring2.Common.Query {

    // Implementation of the query dispatcher - selects and executes the appropriate query
    public class QueryDispatcher : IQueryDispatcher {
	private readonly IServiceProvider provider;

	public QueryDispatcher(IServiceProvider provider) {
	    if (provider == null) {
		throw new ArgumentNullException(nameof(provider));
	    }

	    this.provider = provider;
	}

	public async Task<TResult> Dispatch<TParameter, TResult>(TParameter query)
		where TParameter : IQuery
		where TResult : IQueryResult {
	    IQueryHandler<TParameter, TResult> handler = provider.GetService(typeof(IQueryHandler<TParameter, TResult>)) as IQueryHandler<TParameter, TResult>;
	    Log.Information(handler.GetType().ToString());

	    return await handler.Retrieve(query);
	}
    }
}