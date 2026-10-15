namespace ChurrascariaPDV.Services;

/// <summary>Violação de regra de negócio com mensagem pronta para exibir ao operador.</summary>
public class RegraNegocioException(string mensagem) : Exception(mensagem);
