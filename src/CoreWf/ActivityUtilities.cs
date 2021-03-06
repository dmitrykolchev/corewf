// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using CoreWf.Expressions;
using CoreWf.Runtime;
using CoreWf.Validation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace CoreWf
{
    internal static class ActivityUtilities
    {
        private static Pop s_popActivity = new Pop();
        private static Type s_activityType = typeof(Activity);
        private static Type s_activityGenericType = typeof(Activity<>);
        private static Type s_activityDelegateType = typeof(ActivityDelegate);
        private static Type s_constraintType = typeof(Constraint);
        private static Type s_variableType = typeof(Variable);
        private static Type s_variableGenericType = typeof(Variable<>);
        private static Type s_delegateInArgumentType = typeof(DelegateInArgument);
        private static Type s_delegateOutArgumentType = typeof(DelegateOutArgument);
        private static Type s_delegateInArgumentGenericType = typeof(DelegateInArgument<>);
        private static Type s_delegateOutArgumentGenericType = typeof(DelegateOutArgument<>);
        private static Type s_inArgumentType = typeof(InArgument);
        private static Type s_inArgumentGenericType = typeof(InArgument<>);
        private static Type s_inOutArgumentType = typeof(InOutArgument);
        private static Type s_inOutArgumentGenericType = typeof(InOutArgument<>);
        private static Type s_outArgumentType = typeof(OutArgument);
        private static Type s_outArgumentGenericType = typeof(OutArgument<>);
        private static Type s_argumentType = typeof(Argument);
        private static Type s_argumentReferenceGenericType = typeof(ArgumentReference<>);
        private static Type s_argumentValueGenericType = typeof(ArgumentValue<>);
        private static Type s_runtimeArgumentType = typeof(RuntimeArgument);
        private static Type s_locationGenericType = typeof(Location<>);
        private static Type s_variableReferenceGenericType = typeof(VariableReference<>);
        private static Type s_variableValueGenericType = typeof(VariableValue<>);
        private static Type s_delegateArgumentValueGenericType = typeof(DelegateArgumentValue<>);
        private static Type s_handleType = typeof(Handle);
        private static Type s_iDictionaryGenericType = typeof(IDictionary<,>);
        private static Type s_locationReferenceValueType = typeof(LocationReferenceValue<>);
        private static Type s_environmentLocationValueType = typeof(EnvironmentLocationValue<>);
        private static Type s_environmentLocationReferenceType = typeof(EnvironmentLocationReference<>);
        private static IList<Type> s_collectionInterfaces;
        private static Type s_inArgumentOfObjectType = typeof(InArgument<object>);
        private static Type s_outArgumentOfObjectType = typeof(OutArgument<object>);
        private static Type s_inOutArgumentOfObjectType = typeof(InOutArgument<object>);
        private static PropertyChangedEventArgs s_propertyChangedEventArgs;

        // Can't delay create this one because we use object.ReferenceEquals on it in WorkflowInstance
        private static ReadOnlyDictionary<string, object> s_emptyParameters = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(0));

        public static ReadOnlyDictionary<string, object> EmptyParameters
        {
            get
            {
                return s_emptyParameters;
            }
        }

        internal static PropertyChangedEventArgs ValuePropertyChangedEventArgs
        {
            get
            {
                if (s_propertyChangedEventArgs == null)
                {
                    s_propertyChangedEventArgs = new PropertyChangedEventArgs("Value");
                }
                return s_propertyChangedEventArgs;
            }
        }

        private static IList<Type> CollectionInterfaces
        {
            get
            {
                if (s_collectionInterfaces == null)
                {
                    s_collectionInterfaces = new List<Type>(2)
                        {
                            typeof(IList<>),
                            typeof(ICollection<>)
                        };
                }
                return s_collectionInterfaces;
            }
        }

        public static bool IsInScope(ActivityInstance potentialChild, ActivityInstance scope)
        {
            if (scope == null)
            {
                // No scope means we're in scope
                return true;
            }

            ActivityInstance walker = potentialChild;

            while (walker != null && walker != scope)
            {
                walker = walker.Parent;
            }

            return walker != null;
        }

        public static bool IsHandle(Type type)
        {
            return s_handleType.GetTypeInfo().IsAssignableFrom(type.GetTypeInfo());
        }

        public static bool IsCompletedState(ActivityInstanceState state)
        {
            return state != ActivityInstanceState.Executing;
        }

        public static bool TryGetArgumentDirectionAndType(Type propertyType, out ArgumentDirection direction, out Type argumentType)
        {
            direction = ArgumentDirection.In; // default to In
            argumentType = TypeHelper.ObjectType;  // default to object

            if (propertyType.GetTypeInfo().IsGenericType)
            {
                argumentType = propertyType.GetTypeInfo().GenericTypeArguments[0];

                Type genericType = propertyType.GetGenericTypeDefinition();

                if (genericType == s_inArgumentGenericType)
                {
                    return true;
                }

                if (genericType == s_outArgumentGenericType)
                {
                    direction = ArgumentDirection.Out;
                    return true;
                }

                if (genericType == s_inOutArgumentGenericType)
                {
                    direction = ArgumentDirection.InOut;
                    return true;
                }
            }
            else
            {
                if (propertyType == s_inArgumentType)
                {
                    return true;
                }

                if (propertyType == s_outArgumentType)
                {
                    direction = ArgumentDirection.Out;
                    return true;
                }

                if (propertyType == s_inOutArgumentType)
                {
                    direction = ArgumentDirection.InOut;
                    return true;
                }
            }

            return false;
        }

        public static bool IsArgumentType(Type propertyType)
        {
            return TypeHelper.AreTypesCompatible(propertyType, s_argumentType);
        }

        public static bool IsRuntimeArgumentType(Type propertyType)
        {
            return TypeHelper.AreTypesCompatible(propertyType, s_runtimeArgumentType);
        }

        public static bool IsArgumentDictionaryType(Type type, out Type innerType)
        {
            if (type.GetTypeInfo().IsGenericType)
            {
                bool implementsIDictionary = false;
                Type dictionaryInterfaceType = null;

                if (type.GetGenericTypeDefinition() == s_iDictionaryGenericType)
                {
                    implementsIDictionary = true;
                    dictionaryInterfaceType = type;
                }
                else
                {
                    foreach (Type interfaceType in type.GetInterfaces())
                    {
                        if (interfaceType.GetTypeInfo().IsGenericType &&
                            interfaceType.GetGenericTypeDefinition() == s_iDictionaryGenericType)
                        {
                            implementsIDictionary = true;
                            dictionaryInterfaceType = interfaceType;
                            break;
                        }
                    }
                }

                if (implementsIDictionary == true)
                {
                    Type[] genericArguments = dictionaryInterfaceType.GetGenericArguments();
                    if (genericArguments[0] == TypeHelper.StringType &&
                        IsArgumentType(genericArguments[1]))
                    {
                        innerType = genericArguments[1];
                        return true;
                    }
                }
            }

            innerType = null;
            return false;
        }

        public static bool IsKnownCollectionType(Type type, out Type innerType)
        {
            if (type.GetTypeInfo().IsGenericType)
            {
                if (type.GetTypeInfo().IsInterface)
                {
                    Type localInterface = type.GetGenericTypeDefinition();
                    foreach (Type knownInterface in CollectionInterfaces)
                    {
                        if (localInterface == knownInterface)
                        {
                            Type[] genericArguments = type.GetGenericArguments();
                            if (genericArguments.Length == 1)
                            {
                                innerType = genericArguments[0];
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    // Ask the type whether or not it implements any known collections.
                    Type[] interfaceTypes = type.GetInterfaces();
                    foreach (Type interfaceType in interfaceTypes)
                    {
                        if (interfaceType.GetTypeInfo().IsGenericType)
                        {
                            Type localInterface = interfaceType.GetGenericTypeDefinition();

                            foreach (Type knownInterface in CollectionInterfaces)
                            {
                                if (localInterface == knownInterface)
                                {
                                    Type[] genericArguments = interfaceType.GetGenericArguments();
                                    if (genericArguments.Length == 1)
                                    {
                                        innerType = genericArguments[0];
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            innerType = null;
            return false;
        }

        public static bool IsActivityDelegateType(Type propertyType)
        {
            return TypeHelper.AreTypesCompatible(propertyType, s_activityDelegateType);
        }

        public static bool IsActivityType(Type propertyType)
        {
            return IsActivityType(propertyType, true);
        }

        public static bool IsActivityType(Type propertyType, bool includeConstraints)
        {
            if (!TypeHelper.AreTypesCompatible(propertyType, s_activityType))
            {
                return false;
            }

            // sometimes (for reflection analysis of Activity properties) we don't want constraints to count
            return includeConstraints || !TypeHelper.AreTypesCompatible(propertyType, s_constraintType);
        }

        public static bool TryGetDelegateArgumentDirectionAndType(Type propertyType, out ArgumentDirection direction, out Type argumentType)
        {
            direction = ArgumentDirection.In; // default to In
            argumentType = TypeHelper.ObjectType;  // default to object

            if (propertyType.GetTypeInfo().IsGenericType)
            {
                argumentType = propertyType.GetGenericArguments()[0];

                Type genericType = propertyType.GetGenericTypeDefinition();

                if (genericType == s_delegateInArgumentGenericType)
                {
                    return true;
                }

                if (genericType == s_delegateOutArgumentGenericType)
                {
                    direction = ArgumentDirection.Out;
                    return true;
                }
            }
            else
            {
                if (propertyType == s_delegateInArgumentType)
                {
                    return true;
                }

                if (propertyType == s_delegateOutArgumentType)
                {
                    direction = ArgumentDirection.Out;
                    return true;
                }
            }

            return false;
        }

        public static bool IsVariableType(Type propertyType, out Type innerType)
        {
            if (propertyType.GetTypeInfo().IsGenericType && propertyType.GetGenericTypeDefinition() == s_variableGenericType)
            {
                innerType = propertyType.GetGenericArguments()[0];
                return true;
            }

            innerType = null;
            return TypeHelper.AreTypesCompatible(propertyType, s_variableType);
        }

        public static bool IsVariableType(Type propertyType)
        {
            if (propertyType.GetTypeInfo().IsGenericType && propertyType.GetGenericTypeDefinition() == s_variableGenericType)
            {
                return true;
            }

            return TypeHelper.AreTypesCompatible(propertyType, s_variableType);
        }

        public static bool IsLocationGenericType(Type type, out Type genericArgumentType)
        {
            if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == s_locationGenericType)
            {
                genericArgumentType = type.GetGenericArguments()[0];
                return true;
            }

            genericArgumentType = null;
            return false;
        }

        public static object CreateVariableReference(Variable variable)
        {
            Type genericVariableReferenceType = s_variableReferenceGenericType.MakeGenericType(variable.Type);
            object variableReference = Activator.CreateInstance(genericVariableReferenceType);
            genericVariableReferenceType.GetProperty("Variable").SetValue(variableReference, variable, null);
            return variableReference;
        }

        public static ActivityWithResult CreateLocationAccessExpression(LocationReference locationReference, bool isReference, bool useLocationReferenceValue)
        {
            return LocationAccessExpressionTypeDefinitionsCache.CreateNewLocationAccessExpression(locationReference.Type, isReference, useLocationReferenceValue, locationReference);
        }

        public static Argument CreateArgument(Type type, ArgumentDirection direction)
        {
            Type argumentType = ArgumentTypeDefinitionsCache.GetArgumentType(type, direction);

            Argument argument = (Argument)Activator.CreateInstance(argumentType);

            return argument;
        }

        public static Argument CreateArgumentOfObject(ArgumentDirection direction)
        {
            Argument argument = null;

            if (direction == ArgumentDirection.In)
            {
                argument = (Argument)Activator.CreateInstance(s_inArgumentOfObjectType);
            }
            else if (direction == ArgumentDirection.Out)
            {
                argument = (Argument)Activator.CreateInstance(s_outArgumentOfObjectType);
            }
            else
            {
                argument = (Argument)Activator.CreateInstance(s_inOutArgumentOfObjectType);
            }

            return argument;
        }

        public static Type CreateLocation(Type locationType)
        {
            return s_locationGenericType.MakeGenericType(locationType);
        }

        public static Type CreateActivityWithResult(Type resultType)
        {
            return s_activityGenericType.MakeGenericType(resultType);
        }

        public static Argument CreateReferenceArgument(Type argumentType, ArgumentDirection direction, string referencedArgumentName)
        {
            Argument argument = Argument.Create(argumentType, direction);

            object argumentReference = null;

            if (direction == ArgumentDirection.In)
            {
                // If it is an In then we need an ArgumentValue<T>
                argumentReference = Activator.CreateInstance(s_argumentValueGenericType.MakeGenericType(argumentType), referencedArgumentName);
            }
            else
            {
                // If it is InOut or Out we need an ArgumentReference<T>
                argumentReference = Activator.CreateInstance(s_argumentReferenceGenericType.MakeGenericType(argumentType), referencedArgumentName);
            }

            argument.Expression = (ActivityWithResult)argumentReference;
            return argument;
        }

        public static Variable CreateVariable(string name, Type type, VariableModifiers modifiers)
        {
            Type variableType = s_variableGenericType.MakeGenericType(type);
            Variable variable = (Variable)Activator.CreateInstance(variableType);
            variable.Name = name;
            variable.Modifiers = modifiers;

            return variable;
        }

        // The argumentConsumer is the activity that is attempting to reference the argument
        // with argumentName.  That means that argumentConsumer must be in the Implementation
        // of an activity that defines an argument with argumentName.
        public static RuntimeArgument FindArgument(string argumentName, Activity argumentConsumer)
        {
            if (argumentConsumer.MemberOf != null && argumentConsumer.MemberOf.Owner != null)
            {
                Activity targetActivity = argumentConsumer.MemberOf.Owner;

                for (int i = 0; i < targetActivity.RuntimeArguments.Count; i++)
                {
                    RuntimeArgument argument = targetActivity.RuntimeArguments[i];

                    if (argument.Name == argumentName)
                    {
                        return argument;
                    }
                }
            }

            return null;
        }

        public static string GetDisplayName(object source)
        {
            Fx.Assert(source != null, "caller must verify");
            return GetDisplayName(source.GetType());
        }

        private static string GetDisplayName(Type sourceType)
        {
            if (sourceType.GetTypeInfo().IsGenericType)
            {
                // start with the type name
                string displayName = sourceType.Name;
                int tickIndex = displayName.IndexOf('`');

                // remove the tick+number of parameters "generics format". Note that the
                // tick won't exist for nested implicitly generic classes, such as Foo`1+Bar
                if (tickIndex > 0)
                {
                    displayName = displayName.Substring(0, tickIndex);
                }

                // and provide a more readable version based on the closure type names
                Type[] genericArguments = sourceType.GetGenericArguments();
                StringBuilder stringBuilder = new StringBuilder(displayName);
                stringBuilder.Append("<");
                for (int i = 0; i < genericArguments.Length - 1; i++)
                {
                    stringBuilder.AppendFormat("{0},", GetDisplayName(genericArguments[i]));
                }
                stringBuilder.AppendFormat("{0}>", GetDisplayName(genericArguments[genericArguments.Length - 1]));
                return stringBuilder.ToString();
            }
            else
            {
                Fx.Assert(!sourceType.GetTypeInfo().IsGenericTypeDefinition, "we have an actual object, so we should never have a generic type definition");
                return sourceType.Name;
            }
        }

        internal static void ValidateOrigin(object origin, Activity activity)
        {
            if (origin != null &&
                (origin is Activity || origin is Argument || origin is ActivityDelegate || origin is LocationReference))
            {
                activity.AddTempValidationError(new ValidationError(SR.OriginCannotBeRuntimeIntrinsic(origin)));
            }
        }

        // Returns true if there are any children
        private static void ProcessChildren(Activity parent, IList<Activity> children, ActivityCollectionType collectionType, bool addChildren, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining, ref IList<ValidationError> validationErrors)
        {
            for (int i = 0; i < children.Count; i++)
            {
                Activity childActivity = children[i];
                if (childActivity.InitializeRelationship(parent, collectionType, ref validationErrors))
                {
                    if (addChildren)
                    {
                        SetupForProcessing(childActivity, collectionType != ActivityCollectionType.Imports, ref nextActivity, ref activitiesRemaining);
                    }
                }
            }
        }

        // Note that we do not need an "isPublicCollection" parameter since all arguments are public
        // Returns true if there are any non-null expressions
        private static void ProcessArguments(Activity parent, IList<RuntimeArgument> arguments, bool addChildren, ref ActivityLocationReferenceEnvironment environment, ref int nextEnvironmentId, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining, ref IList<ValidationError> validationErrors)
        {
            if (arguments.Count > 0)
            {
                if (environment == null)
                {
                    environment = new ActivityLocationReferenceEnvironment(parent.GetParentEnvironment());
                }

                for (int i = 0; i < arguments.Count; i++)
                {
                    RuntimeArgument argument = arguments[i];
                    if (argument.InitializeRelationship(parent, ref validationErrors))
                    {
                        argument.Id = nextEnvironmentId;
                        nextEnvironmentId++;

                        // This must be called after InitializeRelationship since it makes
                        // use of RuntimeArgument.Owner;
                        environment.Declare(argument, argument.Owner, ref validationErrors);

                        if (addChildren)
                        {
                            SetupForProcessing(argument, ref nextActivity, ref activitiesRemaining);
                        }
                    }
                }
            }
        }

        // Returns true if there are any non-null defaults
        private static void ProcessVariables(Activity parent, IList<Variable> variables, ActivityCollectionType collectionType, bool addChildren, ref ActivityLocationReferenceEnvironment environment, ref int nextEnvironmentId, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining, ref IList<ValidationError> validationErrors)
        {
            if (variables.Count > 0)
            {
                if (environment == null)
                {
                    environment = new ActivityLocationReferenceEnvironment(parent.GetParentEnvironment());
                }

                for (int i = 0; i < variables.Count; i++)
                {
                    Variable variable = variables[i];
                    if (variable.InitializeRelationship(parent, collectionType == ActivityCollectionType.Public, ref validationErrors))
                    {
                        variable.Id = nextEnvironmentId;
                        nextEnvironmentId++;

                        // This must be called after InitializeRelationship since it makes
                        // use of Variable.Owner;
                        environment.Declare(variable, variable.Owner, ref validationErrors);

                        if (addChildren)
                        {
                            SetupForProcessing(variable, ref nextActivity, ref activitiesRemaining);
                        }
                    }
                }
            }
        }

        // Returns true if there are any non-null handlers
        private static void ProcessDelegates(Activity parent, IList<ActivityDelegate> delegates, ActivityCollectionType collectionType, bool addChildren, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining, ref IList<ValidationError> validationErrors)
        {
            for (int i = 0; i < delegates.Count; i++)
            {
                ActivityDelegate activityDelegate = delegates[i];
                if (activityDelegate.InitializeRelationship(parent, collectionType, ref validationErrors))
                {
                    if (addChildren)
                    {
                        SetupForProcessing(activityDelegate, collectionType != ActivityCollectionType.Imports, ref nextActivity, ref activitiesRemaining);
                    }
                }
            }
        }

        private static void ProcessActivity(ChildActivity childActivity, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining, ActivityCallStack parentChain, ref IList<ValidationError> validationErrors, ProcessActivityTreeOptions options, ProcessActivityCallback callback)
        {
            Fx.Assert(options != null, "options should not be null.");

            if (options.CancellationToken.IsCancellationRequested)
            {
                //throw CoreWf.Internals.FxTrace.Exception.AsError(new OperationCanceledException(options.CancellationToken));
                throw new OperationCanceledException(options.CancellationToken);
            }

            Activity activity = childActivity.Activity;
            IList<Constraint> constraints = activity.RuntimeConstraints;
            IList<ValidationError> tempValidationErrors = null;

            Fx.Assert(validationErrors == null || !options.StoreTempViolations, "Incoming violations should be null if we are storing them in Activity.tempViolations.");

            if (!activity.HasStartedCachingMetadata)
            {
                // We need to add this activity to the IdSpace first so that we have a meaningful ID
                // for any errors that may occur.
                Fx.Assert(activity.MemberOf != null, "We always set this ahead of time - the root is set in InitializeAsRoot and all others are set in InitializeRelationship.");
                activity.MemberOf.AddMember(activity);

                if (TD.InternalCacheMetadataStartIsEnabled())
                {
                    TD.InternalCacheMetadataStart(activity.Id);
                }
                activity.InternalCacheMetadata(options.CreateEmptyBindings, ref tempValidationErrors);
                if (TD.InternalCacheMetadataStopIsEnabled())
                {
                    TD.InternalCacheMetadataStop(activity.Id);
                }

                ActivityValidationServices.ValidateArguments(activity, activity.Parent == null, ref tempValidationErrors);

                ActivityLocationReferenceEnvironment newPublicEnvironment = null;
                ActivityLocationReferenceEnvironment newImplementationEnvironment = new ActivityLocationReferenceEnvironment(activity.HostEnvironment)
                {
                    InternalRoot = activity
                };

                int nextEnvironmentId = 0;

                ProcessChildren(activity, activity.Children, ActivityCollectionType.Public, true, ref nextActivity, ref activitiesRemaining, ref tempValidationErrors);
                ProcessChildren(activity, activity.ImportedChildren, ActivityCollectionType.Imports, true, ref nextActivity, ref activitiesRemaining, ref tempValidationErrors);
                ProcessChildren(activity, activity.ImplementationChildren, ActivityCollectionType.Implementation, !options.SkipPrivateChildren, ref nextActivity, ref activitiesRemaining, ref tempValidationErrors);

                ProcessArguments(activity, activity.RuntimeArguments, true, ref newImplementationEnvironment, ref nextEnvironmentId, ref nextActivity, ref activitiesRemaining, ref tempValidationErrors);

                ProcessVariables(activity, activity.RuntimeVariables, ActivityCollectionType.Public, true, ref newPublicEnvironment, ref nextEnvironmentId, ref nextActivity, ref activitiesRemaining, ref tempValidationErrors);
                ProcessVariables(activity, activity.ImplementationVariables, ActivityCollectionType.Implementation, !options.SkipPrivateChildren, ref newImplementationEnvironment, ref nextEnvironmentId, ref nextActivity, ref activitiesRemaining, ref tempValidationErrors);

                if (activity.HandlerOf != null)
                {
                    // Since we are a delegate handler we have to do some processing
                    // of the handlers parameters.  This is the one part of the tree
                    // walk that actually reaches _up_ to process something we've
                    // already passed.

                    for (int i = 0; i < activity.HandlerOf.RuntimeDelegateArguments.Count; i++)
                    {
                        RuntimeDelegateArgument delegateArgument = activity.HandlerOf.RuntimeDelegateArguments[i];
                        DelegateArgument boundArgument = delegateArgument.BoundArgument;
                        if (boundArgument != null)
                        {
                            // At runtime, delegate arguments end up owned by the Handler
                            // and are scoped like public variables of the handler.
                            //
                            // And since they don't own an expression, there's no equivalent
                            // SetupForProcessing method for DelegateArguments
                            if (boundArgument.InitializeRelationship(activity, ref tempValidationErrors))
                            {
                                boundArgument.Id = nextEnvironmentId;
                                nextEnvironmentId++;
                            }
                        }
                    }
                }

                // NOTE: At this point the declared environment is complete (either we're using the parent or we've got a new one)
                if (newPublicEnvironment == null)
                {
                    activity.PublicEnvironment = new ActivityLocationReferenceEnvironment(activity.GetParentEnvironment());
                }
                else
                {
                    if (newPublicEnvironment.Parent == null)
                    {
                        newPublicEnvironment.InternalRoot = activity;
                    }

                    activity.PublicEnvironment = newPublicEnvironment;
                }

                activity.ImplementationEnvironment = newImplementationEnvironment;

                // ProcessDelegates uses activity.Environment
                ProcessDelegates(activity, activity.Delegates, ActivityCollectionType.Public, true, ref nextActivity, ref activitiesRemaining, ref tempValidationErrors);
                ProcessDelegates(activity, activity.ImportedDelegates, ActivityCollectionType.Imports, true, ref nextActivity, ref activitiesRemaining, ref tempValidationErrors);
                ProcessDelegates(activity, activity.ImplementationDelegates, ActivityCollectionType.Implementation, !options.SkipPrivateChildren, ref nextActivity, ref activitiesRemaining, ref tempValidationErrors);

                if (callback != null)
                {
                    callback(childActivity, parentChain);
                }

                // copy validation errors in ValidationErrors list
                if (tempValidationErrors != null)
                {
                    if (validationErrors == null)
                    {
                        validationErrors = new List<ValidationError>();
                    }
                    Activity source;
                    string prefix = ActivityValidationServices.GenerateValidationErrorPrefix(childActivity.Activity, parentChain, options, out source);

                    for (int i = 0; i < tempValidationErrors.Count; i++)
                    {
                        ValidationError validationError = tempValidationErrors[i];

                        validationError.Source = source;
                        validationError.Id = source.Id;

                        if (!string.IsNullOrEmpty(prefix))
                        {
                            validationError.Message = prefix + validationError.Message;
                        }

                        validationErrors.Add(validationError);
                    }

                    tempValidationErrors = null;
                }

                if (options.StoreTempViolations)
                {
                    if (validationErrors != null)
                    {
                        childActivity.Activity.SetTempValidationErrorCollection(validationErrors);
                        validationErrors = null;
                    }
                }
            }
            else
            {
                // We're processing a reference


                // Add all the children for processing even though they've already
                // been seen.
                SetupForProcessing(activity.Children, true, ref nextActivity, ref activitiesRemaining);
                SetupForProcessing(activity.ImportedChildren, false, ref nextActivity, ref activitiesRemaining);

                SetupForProcessing(activity.RuntimeArguments, ref nextActivity, ref activitiesRemaining);

                SetupForProcessing(activity.RuntimeVariables, ref nextActivity, ref activitiesRemaining);

                SetupForProcessing(activity.Delegates, true, ref nextActivity, ref activitiesRemaining);
                SetupForProcessing(activity.ImportedDelegates, false, ref nextActivity, ref activitiesRemaining);

                if (!options.SkipPrivateChildren)
                {
                    SetupForProcessing(activity.ImplementationChildren, true, ref nextActivity, ref activitiesRemaining);
                    SetupForProcessing(activity.ImplementationDelegates, true, ref nextActivity, ref activitiesRemaining);
                    SetupForProcessing(activity.ImplementationVariables, ref nextActivity, ref activitiesRemaining);
                }

                if (callback != null && !options.OnlyCallCallbackForDeclarations)
                {
                    callback(childActivity, parentChain);
                }

                if (childActivity.Activity.HasTempViolations && !options.StoreTempViolations)
                {
                    childActivity.Activity.TransferTempValidationErrors(ref validationErrors);
                }
            }

            // We only run constraints if the activity could possibly
            // execute and we aren't explicitly skipping them.
            if (!options.SkipConstraints && parentChain.WillExecute && childActivity.CanBeExecuted && constraints.Count > 0)
            {
                ActivityValidationServices.RunConstraints(childActivity, parentChain, constraints, options, false, ref validationErrors);
            }
        }

        // We explicitly call this CacheRootMetadata since it treats the provided
        // activity as the root of the tree.
        public static void CacheRootMetadata(Activity activity, LocationReferenceEnvironment hostEnvironment, ProcessActivityTreeOptions options, ProcessActivityCallback callback, ref IList<ValidationError> validationErrors)
        {
            if (TD.CacheRootMetadataStartIsEnabled())
            {
                TD.CacheRootMetadataStart(activity.DisplayName);
            }
            if (!ShouldShortcut(activity, options))
            {
                lock (activity.ThisLock)
                {
                    if (!ShouldShortcut(activity, options))
                    {
                        if (activity.HasBeenAssociatedWithAnInstance)
                        {
                            //throw CoreWf.Internals.FxTrace.Exception.AsError(new InvalidOperationException(SR.RootActivityAlreadyAssociatedWithInstance(activity.DisplayName)));
                            throw new InvalidOperationException(SR.RootActivityAlreadyAssociatedWithInstance(activity.DisplayName));
                        }

                        activity.InitializeAsRoot(hostEnvironment);

                        ProcessActivityTreeCore(new ChildActivity(activity, true), null, options, callback, ref validationErrors);

                        // Regardless of where the violations came from we only want to
                        // set ourselves RuntimeReady if there are no errors and are
                        // fully cached.
                        if (!ActivityValidationServices.HasErrors(validationErrors) && options.IsRuntimeReadyOptions)
                        {
                            // We don't really support progressive caching at runtime so we only set ourselves
                            // as runtime ready if we cached the whole workflow and created empty bindings.
                            // In order to support progressive caching we need to deal with the following
                            // issues:
                            //   * We need a mechanism for supporting activities which supply extensions
                            //   * We need to understand when we haven't created empty bindings so that
                            //     we can progressively create them
                            //   * We need a mechanism for making sure that we've validated parent related
                            //     constraints at all possible callsites
                            activity.SetRuntimeReady();
                        }
                    }
                }
            }
            if (TD.CacheRootMetadataStopIsEnabled())
            {
                TD.CacheRootMetadataStop(activity.DisplayName);
            }
        }

        // This API is only valid from ProcessActivityCallbacks.  It will cache the rest of the subtree rooted at the
        // provided activity allowing inspection of child metadata before the normal caching pass hits it.
        public static void FinishCachingSubtree(ChildActivity subtreeRoot, ActivityCallStack parentChain, ProcessActivityTreeOptions options)
        {
            IList<ValidationError> discardedValidationErrors = null;
            ProcessActivityTreeCore(subtreeRoot, parentChain, ProcessActivityTreeOptions.GetFinishCachingSubtreeOptions(options), new ProcessActivityCallback(NoOpCallback), ref discardedValidationErrors);
        }

        public static void FinishCachingSubtree(ChildActivity subtreeRoot, ActivityCallStack parentChain, ProcessActivityTreeOptions options, ProcessActivityCallback callback)
        {
            IList<ValidationError> discardedValidationErrors = null;
            ProcessActivityTreeCore(subtreeRoot, parentChain, ProcessActivityTreeOptions.GetFinishCachingSubtreeOptions(options), callback, ref discardedValidationErrors);
        }

        private static void NoOpCallback(ChildActivity element, ActivityCallStack parentChain)
        {
        }

        private static bool ShouldShortcut(Activity activity, ProcessActivityTreeOptions options)
        {
            if (options.SkipIfCached && options.IsRuntimeReadyOptions)
            {
                return activity.IsRuntimeReady;
            }

            return false;
        }

        private static void ProcessActivityTreeCore(ChildActivity currentActivity, ActivityCallStack parentChain, ProcessActivityTreeOptions options, ProcessActivityCallback callback, ref IList<ValidationError> validationErrors)
        {
            Fx.Assert(options != null, "We need you to explicitly specify options.");
            Fx.Assert(currentActivity.Activity.MemberOf != null, "We must have an activity with MemberOf setup or we need to skipIdGeneration.");

            ChildActivity nextActivity = ChildActivity.Empty;
            Stack<ChildActivity> activitiesRemaining = null;

            if (parentChain == null)
            {
                parentChain = new ActivityCallStack();
            }

            if (options.OnlyVisitSingleLevel)
            {
                ProcessActivity(currentActivity, ref nextActivity, ref activitiesRemaining, parentChain, ref validationErrors, options, callback);
            }
            else
            {
                while (!currentActivity.Equals(ChildActivity.Empty))
                {
                    if (object.ReferenceEquals(currentActivity.Activity, s_popActivity))
                    {
                        ChildActivity completedParent = parentChain.Pop();
                        completedParent.Activity.SetCached(isSkippingPrivateChildren: options.SkipPrivateChildren);
                    }
                    else
                    {
                        SetupForProcessing(s_popActivity, true, ref nextActivity, ref activitiesRemaining);
                        ProcessActivity(currentActivity, ref nextActivity, ref activitiesRemaining, parentChain, ref validationErrors, options, callback);
                        parentChain.Push(currentActivity);
                    }

                    // nextActivity is the top of the stack
                    //    stackTop => nextActivity => currentActivity
                    currentActivity = nextActivity;

                    if (activitiesRemaining != null && activitiesRemaining.Count > 0)
                    {
                        nextActivity = activitiesRemaining.Pop();
                    }
                    else
                    {
                        nextActivity = ChildActivity.Empty;
                    }
                }
            }
        }

        private static void SetupForProcessing(IList<Activity> children, bool canBeExecuted, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining)
        {
            for (int i = 0; i < children.Count; i++)
            {
                SetupForProcessing(children[i], canBeExecuted, ref nextActivity, ref activitiesRemaining);
            }
        }

        private static void SetupForProcessing(IList<ActivityDelegate> delegates, bool canBeExecuted, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining)
        {
            for (int i = 0; i < delegates.Count; i++)
            {
                SetupForProcessing(delegates[i], canBeExecuted, ref nextActivity, ref activitiesRemaining);
            }
        }

        private static void SetupForProcessing(IList<Variable> variables, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining)
        {
            for (int i = 0; i < variables.Count; i++)
            {
                SetupForProcessing(variables[i], ref nextActivity, ref activitiesRemaining);
            }
        }

        private static void SetupForProcessing(IList<RuntimeArgument> arguments, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining)
        {
            for (int i = 0; i < arguments.Count; i++)
            {
                SetupForProcessing(arguments[i], ref nextActivity, ref activitiesRemaining);
            }
        }

        private static void SetupForProcessing(ActivityDelegate activityDelegate, bool canBeExecuted, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining)
        {
            if (activityDelegate.Handler != null)
            {
                SetupForProcessing(activityDelegate.Handler, canBeExecuted, ref nextActivity, ref activitiesRemaining);
            }
        }

        private static void SetupForProcessing(Variable variable, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining)
        {
            if (variable.Default != null)
            {
                SetupForProcessing(variable.Default, true, ref nextActivity, ref activitiesRemaining);
            }
        }

        private static void SetupForProcessing(RuntimeArgument argument, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining)
        {
            if (argument.BoundArgument != null && !argument.BoundArgument.IsEmpty)
            {
                SetupForProcessing(argument.BoundArgument.Expression, true, ref nextActivity, ref activitiesRemaining);
            }
        }

        // nextActivity is always the top of the stack
        private static void SetupForProcessing(Activity activity, bool canBeExecuted, ref ChildActivity nextActivity, ref Stack<ChildActivity> activitiesRemaining)
        {
            if (!nextActivity.Equals(ChildActivity.Empty))
            {
                if (activitiesRemaining == null)
                {
                    activitiesRemaining = new Stack<ChildActivity>();
                }

                activitiesRemaining.Push(nextActivity);
            }

            nextActivity = new ChildActivity(activity, canBeExecuted);
        }

        public static void ProcessActivityInstanceTree(ActivityInstance rootInstance, ActivityExecutor executor, Func<ActivityInstance, ActivityExecutor, bool> callback)
        {
            Queue<IList<ActivityInstance>> instancesRemaining = null;

            TreeProcessingList currentInstancesList = new TreeProcessingList();
            currentInstancesList.Add(rootInstance);

            TreeProcessingList nextInstanceList = null;
            if (rootInstance.HasChildren)
            {
                nextInstanceList = new TreeProcessingList();
            }

            while ((instancesRemaining != null && instancesRemaining.Count > 0)
                || currentInstancesList.Count != 0)
            {
                if (currentInstancesList.Count == 0)
                {
                    Fx.Assert(instancesRemaining != null && instancesRemaining.Count > 0, "This must be the clause that caused us to enter");
                    currentInstancesList.Set(instancesRemaining.Dequeue());
                }

                for (int i = 0; i < currentInstancesList.Count; i++)
                {
                    ActivityInstance instance = currentInstancesList[i];

                    if (callback(instance, executor) && instance.HasChildren)
                    {
                        Fx.Assert(nextInstanceList != null, "We should have created this list if we are going to get here.");
                        instance.AppendChildren(nextInstanceList, ref instancesRemaining);
                    }
                }

                if (nextInstanceList != null && nextInstanceList.Count > 0)
                {
                    nextInstanceList.TransferTo(currentInstancesList);
                }
                else
                {
                    // We'll just reuse this object on the next pass (Set will be called)
                    currentInstancesList.Reset();
                }
            }
        }

        public delegate void ProcessActivityCallback(ChildActivity childActivity, ActivityCallStack parentChain);

        public static FaultBookmark CreateFaultBookmark(FaultCallback onFaulted, ActivityInstance owningInstance)
        {
            if (onFaulted != null)
            {
                return new FaultBookmark(new FaultCallbackWrapper(onFaulted, owningInstance));
            }
            return null;
        }

        public static CompletionBookmark CreateCompletionBookmark(CompletionCallback onCompleted, ActivityInstance owningInstance)
        {
            if (onCompleted != null)
            {
                return new CompletionBookmark(new ActivityCompletionCallbackWrapper(onCompleted, owningInstance));
            }
            return null;
        }

        public static CompletionBookmark CreateCompletionBookmark(DelegateCompletionCallback onCompleted, ActivityInstance owningInstance)
        {
            if (onCompleted != null)
            {
                return new CompletionBookmark(new DelegateCompletionCallbackWrapper(onCompleted, owningInstance));
            }
            return null;
        }

        public static CompletionBookmark CreateCompletionBookmark<TResult>(CompletionCallback<TResult> onCompleted, ActivityInstance owningInstance)
        {
            if (onCompleted != null)
            {
                return new CompletionBookmark(new FuncCompletionCallbackWrapper<TResult>(onCompleted, owningInstance));
            }

            return null;
        }

        public static string GetTraceString(Bookmark bookmark)
        {
            if (bookmark.IsNamed)
            {
                return "'" + bookmark.Name + "'";
            }
            else
            {
                return string.Format(CultureInfo.InvariantCulture, "<Unnamed Id={0}>", bookmark.Id);
            }
        }

        public static string GetTraceString(BookmarkScope bookmarkScope)
        {
            if (bookmarkScope == null)
            {
                return "<None>";
            }
            else if (bookmarkScope.IsInitialized)
            {
                return "'" + bookmarkScope.Id.ToString() + "'";
            }
            else
            {
                return string.Format(CultureInfo.InvariantCulture, "<Uninitialized TemporaryId={0}>", bookmarkScope.TemporaryId);
            }
        }

        public static void RemoveNulls(IList list)
        {
            if (list != null)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] == null)
                    {
                        list.RemoveAt(i);
                    }
                }
            }
        }

        public static void Add<T>(ref Collection<T> collection, T data)
        {
            if (data != null)
            {
                if (collection == null)
                {
                    collection = new Collection<T>();
                }
                collection.Add(data);
            }
        }

        public static void Add<T>(ref IList<T> list, T data)
        {
            if (data != null)
            {
                if (list == null)
                {
                    list = new List<T>();
                }
                list.Add(data);
            }
        }

        public class TreeProcessingList
        {
            private ActivityInstance _singleItem;
            private IList<ActivityInstance> _multipleItems;
            private bool _addRequiresNewList;

            public TreeProcessingList()
            {
            }

            public int Count
            {
                get
                {
                    if (_singleItem != null)
                    {
                        return 1;
                    }

                    if (_multipleItems != null)
                    {
                        return _multipleItems.Count;
                    }

                    return 0;
                }
            }

            public ActivityInstance this[int index]
            {
                get
                {
                    if (_singleItem != null)
                    {
                        Fx.Assert(index == 0, "We expect users of TreeProcessingList never to be out of range.");
                        return _singleItem;
                    }
                    else
                    {
                        Fx.Assert(_multipleItems != null, "Users shouldn't call this if we have no items.");
                        Fx.Assert(_multipleItems.Count > index, "Users should never be out of range.");

                        return _multipleItems[index];
                    }
                }
            }

            public void Set(IList<ActivityInstance> listToSet)
            {
                Fx.Assert(_singleItem == null && (_multipleItems == null || _multipleItems.Count == 0), "We should not have any items if calling set.");

                _multipleItems = listToSet;
                _addRequiresNewList = true;
            }

            public void Add(ActivityInstance item)
            {
                if (_multipleItems != null)
                {
                    if (_addRequiresNewList)
                    {
                        _multipleItems = new List<ActivityInstance>(_multipleItems);
                        _addRequiresNewList = false;
                    }

                    _multipleItems.Add(item);
                }
                else if (_singleItem != null)
                {
                    _multipleItems = new List<ActivityInstance>(2);
                    _multipleItems.Add(_singleItem);
                    _multipleItems.Add(item);
                    _singleItem = null;
                }
                else
                {
                    _singleItem = item;
                }
            }

            // Because of how we use this we don't need a Clear().
            // Basically we gain nothing by clearing the multipleItems
            // list and hanging onto it.
            public void Reset()
            {
                _addRequiresNewList = false;
                _multipleItems = null;
                _singleItem = null;
            }

            public void TransferTo(TreeProcessingList otherList)
            {
                otherList._singleItem = _singleItem;
                otherList._multipleItems = _multipleItems;
                otherList._addRequiresNewList = _addRequiresNewList;

                Reset();
            }
        }

        // We don't implement anything in this class.  We just use it as
        // a placeholder for when to pop off our parent stack.
        private class Pop : Activity
        {
            internal override void InternalExecute(ActivityInstance instance, ActivityExecutor executor, BookmarkManager bookmarkManager)
            {
                throw Fx.AssertAndThrow("should never get here");
            }

            protected override void CacheMetadata(ActivityMetadata metadata)
            {
                throw Fx.AssertAndThrow("should never get here");
            }

            internal override void OnInternalCacheMetadata(bool createEmptyBindings)
            {
                throw Fx.AssertAndThrow("should never get here");
            }
        }

        public struct ChildActivity : IEquatable<ChildActivity>
        {
            public ChildActivity(Activity activity, bool canBeExecuted)
                : this()
            {
                Activity = activity;
                CanBeExecuted = canBeExecuted;
            }

            public static ChildActivity Empty
            {
                get
                {
                    return new ChildActivity();
                }
            }

            public Activity Activity
            {
                get;
                set;
            }

            public bool CanBeExecuted
            {
                get;
                set;
            }

            public bool Equals(ChildActivity other)
            {
                return object.ReferenceEquals(Activity, other.Activity) && CanBeExecuted == other.CanBeExecuted;
            }
        }

        public class ActivityCallStack
        {
            private int _nonExecutingParentCount;
            private Quack<ChildActivity> _callStack;

            public ActivityCallStack()
            {
                _callStack = new Quack<ChildActivity>();
            }

            public bool WillExecute
            {
                get
                {
                    return _nonExecutingParentCount == 0;
                }
            }

            public ChildActivity this[int index]
            {
                get
                {
                    return _callStack[index];
                }
            }

            public int Count
            {
                get
                {
                    return _callStack.Count;
                }
            }

            public void Push(ChildActivity childActivity)
            {
                if (!childActivity.CanBeExecuted)
                {
                    _nonExecutingParentCount++;
                }

                _callStack.PushFront(childActivity);
            }

            public ChildActivity Pop()
            {
                ChildActivity childActivity = _callStack.Dequeue();

                if (!childActivity.CanBeExecuted)
                {
                    _nonExecutingParentCount--;
                }

                return childActivity;
            }
        }

        private static class ArgumentTypeDefinitionsCache
        {
            private static Dictionary<Type, Type> s_inArgumentTypeDefinitions = new Dictionary<Type, Type>();
            private static Dictionary<Type, Type> s_outArgumentTypeDefinitions = new Dictionary<Type, Type>();
            private static Dictionary<Type, Type> s_inOutArgumentTypeDefinitions = new Dictionary<Type, Type>();

            public static Type GetArgumentType(Type type, ArgumentDirection direction)
            {
                Dictionary<Type, Type> lookupTable = null;

                if (direction == ArgumentDirection.In)
                {
                    lookupTable = s_inArgumentTypeDefinitions;
                }
                else if (direction == ArgumentDirection.Out)
                {
                    lookupTable = s_outArgumentTypeDefinitions;
                }
                else
                {
                    lookupTable = s_inOutArgumentTypeDefinitions;
                }

                Type argumentType;
                if (!lookupTable.TryGetValue(type, out argumentType))
                {
                    argumentType = CreateArgumentType(type, direction);
                    lock (lookupTable)
                    {
                        lookupTable[type] = argumentType;
                    }
                }

                return argumentType;
            }

            private static Type CreateArgumentType(Type type, ArgumentDirection direction)
            {
                Type argumentType = null;

                if (direction == ArgumentDirection.In)
                {
                    argumentType = ActivityUtilities.s_inArgumentGenericType.MakeGenericType(type);
                }
                else if (direction == ArgumentDirection.Out)
                {
                    argumentType = ActivityUtilities.s_outArgumentGenericType.MakeGenericType(type);
                }
                else
                {
                    argumentType = ActivityUtilities.s_inOutArgumentGenericType.MakeGenericType(type);
                }

                return argumentType;
            }
        }

        private static class LocationAccessExpressionTypeDefinitionsCache
        {
            private static object s_locationReferenceValueTypeDefinitionsLock = new object();
            private static Dictionary<Type, ILocationReferenceExpression> s_locationReferenceValueTypeDefinitions = new Dictionary<Type, ILocationReferenceExpression>();

            private static object s_environmentLocationReferenceTypeDefinitionsLock = new object();
            private static Dictionary<Type, ILocationReferenceExpression> s_environmentLocationReferenceTypeDefinitions = new Dictionary<Type, ILocationReferenceExpression>();

            private static object s_environmentLocationValueTypeDefinitionsLock = new object();
            private static Dictionary<Type, ILocationReferenceExpression> s_environmentLocationValueTypeDefinitions = new Dictionary<Type, ILocationReferenceExpression>();

            public static ActivityWithResult CreateNewLocationAccessExpression(Type type, bool isReference, bool useLocationReferenceValue, LocationReference locationReference)
            {
                Dictionary<Type, ILocationReferenceExpression> lookupTable = null;
                object tableLock = null;

                if (useLocationReferenceValue)
                {
                    lookupTable = s_locationReferenceValueTypeDefinitions;
                    tableLock = s_locationReferenceValueTypeDefinitionsLock;
                }
                else
                {
                    lookupTable = isReference ? s_environmentLocationReferenceTypeDefinitions : s_environmentLocationValueTypeDefinitions;
                    tableLock = isReference ? s_environmentLocationReferenceTypeDefinitionsLock : s_environmentLocationValueTypeDefinitionsLock;
                }

                ILocationReferenceExpression existingInstance;
                lock (tableLock)
                {
                    if (!lookupTable.TryGetValue(type, out existingInstance))
                    {
                        Type locationAccessExpressionType = CreateLocationAccessExpressionType(type, isReference, useLocationReferenceValue);

                        // Create an "empty" (locationReference = null) instance to put in the cache. This empty instance will only be used to create other instances,
                        // including the instance returned from this method. The cached instance will never be included in an activity tree, so the cached instance's
                        // rootActivity field will not be filled in and thus will not pin all the objects in the activity tree. The cached empty instance has a null
                        // locationReference because locationReference also pins parts of activity tree.
                        //existingInstance = (ILocationReferenceExpression)Activator.CreateInstance(
                        //    locationAccessExpressionType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, null);

                        foreach (var constructor in locationAccessExpressionType.GetTypeInfo().DeclaredConstructors)
                        {
                            if (constructor.GetParameters().Length == 0)
                            {
                                existingInstance = (ILocationReferenceExpression)constructor.Invoke(null);
                                break;
                            }
                        }
                        lookupTable[type] = existingInstance;
                    }
                }

                return existingInstance.CreateNewInstance(locationReference);
            }

            private static Type CreateLocationAccessExpressionType(Type type, bool isReference, bool useLocationReferenceValue)
            {
                Type openType;
                if (useLocationReferenceValue)
                {
                    openType = s_locationReferenceValueType;
                }
                else
                {
                    openType = isReference ? s_environmentLocationReferenceType : s_environmentLocationValueType;
                }

                return openType.MakeGenericType(type);
            }
        }
    }
}
