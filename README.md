Rikrop.Core.Framework
=====================
Библиотека содержит методы для помощи в регистрации объектов внутри контейнера. Несмотря на небольшой набор открытых методов, Rikrop.Core.Framework.Unity.dll составляет центральную часть любого нашего приложения. Рассмотрим всё по порядку.
namespace Rikrop.Core.Framework.Unity
---------------------
Это пространство имен содержит единственный метод-расширение для регистрации провайдеров даты-времени в приложении:
```
public static IUnityContainer RegisterDateTimeProviders(this IUnityContainer container) { ... }
```
namespace Rikrop.Core.Framework.Unity.Factories
---------------------
Здесь расположена интереснейшая реализация регистрации фабрик создания объектов и регистрации этих фабрик в контейнере приложения.
Мы стараемся уменьшить количество кода при разработке, поэтому уделили особое внимание этой теме. Публичный интерфейс на первый взгляд ни о чём не говорит:
```
public static class FactoryExtension
{
    public static void RegisterFactory<TFactory>(this IUnityContainer container) { ... }
    public static void RegisterFactory(this IUnityContainer container, Type factoryInterfaceType) { ... }
    public static void RegisterAutoFactories(this IUnityContainer container, Assembly assembly) { ... }
}
```
Гораздо интереснее посмотреть на применение этих методов. Такую запись можно встретить во многих наших классах:
```
public class IndividualCalendarViewModel : ApplyWorkspace<IndividualCalendarView>
{
    ...
     public IndividualCalendarViewModel(long employeeId,
         IndividualStatisticsViewModel.ICtor individualStatisticsViewModel,
         IndividualScheduleViewModel.ICtor individualSheduleViewModelCtor)
    {
        _individualStatisticsViewModel = individualStatisticsViewModel.Create(employeeId);
        _individualSheduleViewModel = individualSheduleViewModelCtor.Create();
        ...
    }
 
    ...
 
    public interface ICtor
    {
        IndividualCalendarViewModel Create(long employeeId);
    }
}
```
А следующим образом можно зарегистрировать фабрики в контейнере:
```
foreach (var type in assemblies.SelectMany(assembly => assembly.DefinedTypes.Where(type => type.Name == "ICtor")))
{
    if (!container.IsRegistered(type))
    {
        container.RegisterFactory(type);
    }
}
```
Или в случае с использованием авотматических фабрик:
```
foreach (var assembly in assemblies)
{
    container.RegisterAutoFactories(assembly);
}
```
Как это работает?
---------------------
Нужно заметить, что для создания фабрики объекта не нужно писать какой-либо дополнительный код. При этом объект нужного типа создаётся при помощи вызова метода  Create:
```
_individualSheduleViewModel = individualSheduleViewModelCtor.Create();
```
На самом деле при регистрации фабрики в контейнере мы проделываем всю необходимую работу за программиста - в памяти создаётся динамическая сборка, которая регистрируется в домене приложения. При помощи пространства имен System.Reflection.Emit мы генерируем IL-код с реальной фабрикой, создаем конструктор фабрики, принимающий на вход контейнер приложения и методы создания реального объекта с разрешением всех необходимых зависимостей.
Всё это звучит запутано и абстрактно, но мы хотим проиллюстрировать ту простоту, с которой решается одна из сложных проблем внутри генератора фабрик. В примере выше можно увидеть, что набор параметров конструктора отличается от набора параметров в методе Create для фабрики:
```
public class IndividualCalendarViewModel : ApplyWorkspace<IndividualCalendarView>
{
     public IndividualCalendarViewModel(long employeeId,
         IndividualStatisticsViewModel.ICtor individualStatisticsViewModel,
         IndividualScheduleViewModel.ICtor individualSheduleViewModelCtor)
    {
        ...
    }
 
    public interface ICtor
    {
        IndividualCalendarViewModel Create(long employeeId);
    }
}
```
Фабрика позволяет передавать часть параметров в конструктор типа явно, остальные параметры будут разрешаться из контейнера приложения.
```
private static void GenerateCreateMethods(TypeInfo typeInfo, MethodInfo resolveMethodInfo)
{
    // Для каждого метода в фабрике
    foreach (var factoryMethodInfo in typeInfo.FactoryInterfaceType.GetMethods())
    {
        // Извлечение возвращаемого параметра - целевого типа, объект которого будет создан
        Type targetType = GetTargetType(factoryMethodInfo);
 
        // Извлечение конструктора целевого типа
        ConstructorInfo targetTypeConstructor = FindTargetTypeConstructor(targetType);
 
        // Извлечение параметров фабричного метода
        ParameterInfo[] methodParametersInfo = factoryMethodInfo.GetParameters();
 
        // Построитель фактического метода создания объекта
        MethodBuilder methodBuilder = typeInfo.TypeBuilder.DefineMethod(factoryMethodInfo.Name, MethodAttributes.Public | MethodAttributes.Virtual, CallingConventions.HasThis,
                                                                factoryMethodInfo.ReturnType, methodParametersInfo.Select(p => p.ParameterType).ToArray());
 
        // Генератор IL-кода для метода
        ILGenerator il = methodBuilder.GetILGenerator();
 
        // Маппер параметров фабричного метода и конструктора целвого типа
        Mapper mapper = new Mapper(methodParametersInfo);
 
        int factoryMethodParameterIndex = 0;
 
        // Для каждого параметра конструктора целевого типа
        foreach (ParameterInfo targetTypeConstructorPrmInfo in targetTypeConstructor.GetParameters())
        {
            // Поиск соответствия параметру фабричного метода
            var factoryMethodParameterInfo = mapper.FindMethodParameterForConstructorParameter(targetTypeConstructorPrmInfo);
            if (factoryMethodParameterInfo == null)
            {
                // Если соответствие не найдено, генерируем код разрешения параметра из контейнера
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, targetTypeConstructorPrmInfo.Name);
                il.Emit(OpCodes.Call, resolveMethodInfo.MakeGenericMethod(targetTypeConstructorPrmInfo.ParameterType));
            }
            else
            {
                // Передаём параметр фабричного метода в конструктор
                EmitLdarg(il, factoryMethodParameterIndex);
                factoryMethodParameterIndex++;
            }
        }
 
        il.Emit(OpCodes.Newobj, targetTypeConstructor);
        il.Emit(OpCodes.Ret);
    }
}
```
В результате, если конструктор объекта выглядит следующим образом:
```
public SomeClass(ILogger logger, int intPrm) { ... }
```
, а фабричный метод следующим:
```
public SomeClass Create(int intPrm);
```
, то генератор фабрики сформирует такую конструкцию:
```
public SomeClass Create(int intPrm)
{
    return new SomeClass(
        ResolveParameterWithException<ILogger>("logger"),
        intPrm);
}
```
Зачем это использовать
---------------------
К счастью, красота реализации фабрик в нашем фреймворке скрыта модификаторами доступа классов внутри библиотеки. Использовать же мощь фабрик проще простого. Пример регистрации фабрик в контейнере был приведен выше.
Конечно, альтернативой к автогенерации фабрик служит создание интерфейса для каждой сущности, регистрируемой в контейнере. Можно обойтись и вовсе без интерфейсов, но тогда внедрение зависимости теряет всякий смысл.
Нам кажется удобным использование автогенерируемых фабрик по нескольким причинам.
Во-первых, это быстро - достаточно написать несколько строк и фабрика готова:
```
public interface ICtor
{
    PunchViewModel Create();
}
```
во-вторых, это гораздо универсальнее, чем просто объект - часто объект не может быть создан, пока не определены некоторые параметры:
```
public class PunchViewModel : Workspace<PunchView>
{
    private readonly EditDayActivityWorkspace.ICtor _editDayActivityWorkspaceCtor;
 
    public PunchViewModel(EditDayActivityWorkspace.ICtor editDayActivityWorkspaceCtor)
    {
        _editDayActivityWorkspaceCtor = editDayActivityWorkspaceCtor;
             
        ...
    }
 
    private async void ChangeState(long initiatorId)
    {
        var workspace = _editDayActivityWorkspaceCtor.Create(initiatorId);
 
        ...
    }
}
```
В-третьих, это значительно уменьшает количество кода. Не нужно создавать интерфейс, добавлять его регистрацию в контейнер. А в итоге не иметь гибкости, которую даёт фабрика.
В-четвертых, с помощью авторегистрации фабрик код приложения можно полностью отделить от внедрения зависимости - больше никаких антипаттернов.
В-пятых, это тестируемо:
```
var loginInfo = Mock.Create<LoginInfo>();
Mock.Arrange(() => loginInfo.AutoLogin).Returns(true);
Mock.Arrange(() => loginInfo.CurrentUserName).Returns("AnyUserName");
 
var loginInfoCtor = Mock.Create<LoginInfo.ICtor>(Behavior.Strict);
Mock.Arrange(() => loginInfoCtor.Create()).Returns(loginInfo);
```
В-шестых, это универсально. Фабрика может содержать несколько методов создания объекта с различным набором параметров. Фабрика может содержать методы, создающие объекты разных типов, а не только типа, в котором она определена. Таким образом, можно реализовать фабрику фабрик и больше никогда не торговать молотками!
А можно еще проще?
---------------------
Ну, конечно! Контракт по имени интерфейса удобен, но не всегда возможен и оправдан. Нам захотелось сделать свою жизнь проще - и мы сделали:
```
[AttributeUsage(AttributeTargets.Interface)]
public class AutoFactoryAttribute : Attribute
{
}
```
```
public static void RegisterAutoFactories(this IUnityContainer container, Assembly assembly)
{
    foreach (var interfaceType in assembly.GetTypes().Where(type => type.IsInterface))
    {
        if (interfaceType.GetCustomAttribute<AutoFactoryAttribute>() != null)
        {
            container.RegisterFactory(interfaceType);
        }
    }
}
```
```
public class WorkplacesViewModel
{
    [AutoFactory]
    public interface IWorkplacesViewModel
    {
        WorkplacesViewModel Create();
    }
}
```
Способ создания и регистрации фабрик, реализованный в библиотеке  Rikrop.Core.Framework.Unity.dll очень помогает нам ускорить разработку и очистить код от мусорных интерфейсов, которые в DI используются зачастую исключительно для реализации регистрации в контейнере. Вместе с тем нужно помнить, что интерфейс служит прежде всего для определения контракта, и эту функцию фабрики тоже должны поддерживать.
```
public interface IWorkplacesViewModel
{
    IEnumerable<Workplace> Workplaces { get; }
}
 
public class ReadOnlyWorkplacesViewModel : IWorkplacesViewModel
{
    ...
 
    [AutoFactory]
    public interface IWorkplacesViewModel
    {
        [Creates(typeof(ReadOnlyWorkplacesViewModel))]
        IWorkplacesViewModel Create();
    }
}
 
public class EditableWorkplacesViewModel : IWorkplacesViewModel
{
    ...
 
    [AutoFactory]
    public interface IWorkplacesViewModel
    {
        [Creates(typeof(EditableWorkplacesViewModel))]
        IWorkplacesViewModel Create();
    }
}
```
А можно еще проще?
---------------------
Наверняка, да. Мы выбрали баланс между гибкостью и простотой. Созданный инструмент позволил нам сосредоточиться при разработке на задачах развития функционала и поддержания целостной архитектуры приложения, не отвлекаясь на написание избыточного кода, необходимо для явной реализации dependency injection.
