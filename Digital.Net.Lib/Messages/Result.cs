namespace Digital.Net.Lib.Messages;

public class Result
{
    public List<ResultMessage> Errors { get; init; } = [];
    public List<ResultMessage> Infos { get; init; } = [];

    public bool HasError => Errors.Count > 0;

    public bool HasErrorOfType<TException>() where TException : Exception
    {
        var result = Errors.Any(error => error.IsExceptionOfType<TException>());
        return result;
    }

    public Result Merge(Result result)
    {
        Errors.AddRange(result.Errors);
        Infos.AddRange(result.Infos);
        return this;
    }

    public Result AddError(Exception ex)
    {
        Errors.Add(new ResultMessage(ex));
        return this;
    }

    public Result ClearErrors()
    {
        Errors.Clear();
        return this;
    }

    public Result AddInfo(string message)
    {
        Infos.Add(new ResultMessage(message));
        return this;
    }
    
    public TReturn? Try<TReturn>(Func<Result<TReturn>> action)
    {
        try
        {
            var result = action();
            Merge(result);
            return result.Value;
        }
        catch (Exception ex)
        {
            AddError(ex);
            return default;
        }
    }

    public async Task<TReturn?> TryAsync<TReturn>(Func<Task<Result<TReturn>>> action)
    {
        try
        {
            var result = await action();
            Merge(result);
            return result.Value;
        }
        catch (Exception ex)
        {
            AddError(ex);
            return default;
        }
    }
}

public class Result<T> : Result
{
    public Result()
    {
    }

    public Result(T value)
    {
        Value = value;
    }

    public T? Value { get; set; }

    public new Result<T> Merge(Result result)
    {
        base.Merge(result);
        return this;
    }

    public new Result<T> AddError(Exception ex)
    {
        base.AddError(ex);
        return this;
    }

    public new Result<T> AddInfo(string message)
    {
        base.AddInfo(message);
        return this;
    }
}