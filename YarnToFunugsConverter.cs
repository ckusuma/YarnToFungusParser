using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CsvHelper;
using Yarn;
using Yarn.Unity;
using Fungus;
using System;

public class YarnToFunugsConverter : DialogueUIBehaviour {

    #region VARIABLES

    // put all gameobjects which you want flowcharts on in here
    [SerializeField]
    List<GameObject> flowchartObjects = new List<GameObject>();
    int flowchartObjectCounter = 0;

    // current flowchart object that is being passed in information
    [SerializeField]
    GameObject currentFlowchartObj;

    //a reference to the dialogue runner
    [SerializeField]
    DialogueRunner runner;

    // list of yarn files to be parsed
    [SerializeField]
    TextAsset[] yarnFiles;

    Dictionary<string, Flowchart> flowcharts = new Dictionary<string, Flowchart>();
    
    #endregion

    #region UNITY FUNCTIONS
    private void OnEnable()
    {
        DialogueRunner.nodesParsedEvent += OnNodesParsed;
    }

    private void OnDisable()
    {
        DialogueRunner.nodesParsedEvent -= OnNodesParsed;
    }
    #endregion

    #region YARN PARSING
    // Subscribes to "OnNodesParsed" event in DialogueRunner. 
    public void OnNodesParsed(Dictionary<string, Yarn.Parser.Node> nodes, string name)
    {
        // TODO: start a new flowchart at the start of this function and change the reference to the current flowchart
        //flowcharts[name] = new Flowchart();
        ParseNodes(nodes, name);
    }

    // Starts up node parsing
    public void ParseNodes(Dictionary<string, Yarn.Parser.Node> nodes, string name)
    {
        Dictionary<string, Block> blocks = new Dictionary<string, Block>();
        ParseNode(nodes["Start"], nodes, blocks, name);
        foreach (KeyValuePair<string, Yarn.Parser.Node> node in nodes)
        {
            if (!node.Key.Equals("Start"))
            {
                ParseNode(node.Value, nodes, blocks, name);
            }
        }
    }

    // Recursively parses the node
    public void ParseNode(Yarn.Parser.Node node, Dictionary<string, Yarn.Parser.Node> nodes, Dictionary<string, Block> blocks, string name)
    {
        if (!blocks.ContainsKey(node.name))
        {
            Block b = currentFlowchartObj.AddComponent<Block>();
            b.BlockName = node.name;
            blocks[node.name] = b;
            ParseNodeHelper(node, b, nodes, blocks);
        } 
        else
        {
            ParseNodeHelper(node, blocks[node.name], nodes, blocks);
        }
    }

    public void ParseNodeHelper(Yarn.Parser.Node n, Block block, Dictionary<string, Yarn.Parser.Node> nodes, Dictionary<string, Block> blocks)
    {
        List<Parser.Statement> commands = n._statements;
        foreach (Parser.Statement statement in commands)
        {
            if (statement.line != null)
            {
                CreateSay(statement.line, block);
            }
            else if (statement.optionStatement != null)
            {
                CreateCallCommand(statement.optionStatement.destination, block, blocks);
            }
            else if (statement.customCommand != null)
            {
                CreateCustomCommand(statement.customCommand.clientCommand, block);
            }
            else if (statement.shortcutOptionGroup != null)
            {
                CreateOptionGroup(statement.shortcutOptionGroup._options, block, nodes, blocks);
            }
            else if (statement.ifStatement != null)
            {
                List<Parser.IfStatement.Clause> clauses = statement.ifStatement.clauses;
                CreateConditional(clauses, block, nodes, blocks);
            }
        }
    }

    public void ParseNodeHelper(IEnumerable<Parser.Statement> statements, Block block, Dictionary<string, Yarn.Parser.Node> nodes, Dictionary<string, Block> blocks)
    {
        foreach (Parser.Statement statement in statements)
        {
            if (statement.line != null)
            {
                CreateSay(statement.line, block);
            }
            else if (statement.optionStatement != null)
            {
                CreateCallCommand(statement.optionStatement.destination, block, blocks);
            }
            else if (statement.customCommand != null)
            {
                CreateCustomCommand(statement.customCommand.clientCommand, block);
            }
            else if (statement.shortcutOptionGroup != null)
            {
                CreateOptionGroup(statement.shortcutOptionGroup._options, block, nodes, blocks);
            }
            else if (statement.ifStatement != null)
            {
                List<Parser.IfStatement.Clause> clauses = statement.ifStatement.clauses;
                CreateConditional(clauses, block, nodes, blocks);
            }
            else if (statement.assignmentStatement != null)
            {
                CreateAssignment(statement.assignmentStatement, block, nodes, blocks);
            }
        }
    }
    #endregion

    #region FUNGUS ELEMENT CREATION

    public void CreateAssignment(Parser.AssignmentStatement ass, Block block, Dictionary<string, Yarn.Parser.Node> nodes, Dictionary<string, Block> blocks)
    {
        SetVariable setVar = currentFlowchartObj.AddComponent<SetVariable>();
        Flowchart currFlowchart = currentFlowchartObj.GetComponent<Flowchart>();

        switch (ass.operation)
        {
            case Yarn.TokenType.EqualToOrAssign:
                setVar.SetSetOperator(Fungus.SetOperator.Assign);
                break;
            case Yarn.TokenType.AddAssign:
                setVar.SetSetOperator(Fungus.SetOperator.Add);
                break;
            case Yarn.TokenType.MinusAssign:
                setVar.SetSetOperator(SetOperator.Subtract);
                break;
            case Yarn.TokenType.DivideAssign:
                setVar.SetSetOperator(SetOperator.Divide);
                break;
            case Yarn.TokenType.MultiplyAssign:
                setVar.SetSetOperator(SetOperator.Multiply);
                break;
            default:
                Debug.LogError("Unknown Operator");
                break;
        }
        switch (ass.valueExpression.value.value.type)
        {
            case Value.Type.String:
                StringDataMulti sdm = new StringDataMulti(ass.valueExpression.value.value.AsString);
                setVar.SetStringData(sdm);
                StringVariable sv = null;
                if (currFlowchart.GetVariable<StringVariable>(ass.destinationVariableName) == null)
                {
                    sv = currentFlowchartObj.AddComponent<StringVariable>();
                    sv.Scope = VariableScope.Public;
                    sv.Key = ass.destinationVariableName;
                    sv.Value = "";
                    currFlowchart.AddVariable(sv);
                    currFlowchart.SetStringVariable(sv.Key, sv.Value);
                }
                else
                {
                    sv = currFlowchart.GetVariable<StringVariable>(ass.destinationVariableName);
                    currFlowchart.SetStringVariable(sv.Key, sv.Value);
                }
                setVar.SetAffectedVariable(sv);
                break;
            case Value.Type.Number:
                FloatData fd = new FloatData(ass.valueExpression.value.value.AsNumber);
                setVar.SetFloatData(fd);
                FloatVariable fv = null;
                if (currFlowchart.GetVariable<FloatVariable>(ass.destinationVariableName) == null)
                {
                    fv = currentFlowchartObj.AddComponent<FloatVariable>();
                    fv.Scope = VariableScope.Public;
                    fv.Key = ass.destinationVariableName;
                    fv.Value = 0;
                    currFlowchart.AddVariable(fv);
                    currFlowchart.SetFloatVariable(fv.Key, fv.Value);
                }
                else
                {
                    fv = currFlowchart.GetVariable<FloatVariable>(ass.destinationVariableName);
                    currFlowchart.SetFloatVariable(fv.Key, fv.Value);
                }
                setVar.SetAffectedVariable(fv);
                break;
            case Value.Type.Bool:
                BooleanData bd = new BooleanData(ass.valueExpression.value.value.AsBool);
                setVar.SetBooleanData(bd);
                BooleanVariable bv = null;
                if (currFlowchart.GetVariable<BooleanVariable>(ass.destinationVariableName) == null)
                {
                    bv = currentFlowchartObj.AddComponent<BooleanVariable>();
                    bv.Scope = VariableScope.Public;
                    bv.Key = ass.destinationVariableName;
                    bv.Value = false;
                    currFlowchart.AddVariable(bv);
                    currFlowchart.SetBooleanVariable(bv.Key, bv.Value);
                }
                else
                {
                    bv = currFlowchart.GetVariable<BooleanVariable>(ass.destinationVariableName);
                    currFlowchart.SetBooleanVariable(bv.Key, bv.Value);
                }
                setVar.SetAffectedVariable(bv);
                break;
            default:
                Debug.LogError("Unknown type");
                break;
        }

        block.CommandList.Add(setVar);

    }

    public void CreateConditional(List<Parser.IfStatement.Clause> clauses, Block block, Dictionary<string, Yarn.Parser.Node> nodes, Dictionary<string, Block> blocks)
    {
        /**
         * NOTES:
         *  - clause.expression will be null if it's an else statement
         *  - refer to DialogueRunner for examples of parsing
         */
        for (int i = 0; i < clauses.Count; i++)
        {
            Parser.IfStatement.Clause clause = clauses[i];
            //if the expression is null, it is an else statement
            if (clause.expression == null)
            {
                Else elseStatement = currentFlowchartObj.AddComponent<Else>();
                block.CommandList.Add(elseStatement);
            }
            // if the clause is the first entry in clauses, then it is an if statement
            else if (i == 0)
            {
                If ifstatement = currentFlowchartObj.AddComponent<If>();
                block.CommandList.Add(ifstatement);
                if (clause.expression.function != null)
                {
                    //it is an operator statement
                    switch (clause.expression.function.name)
                    {
                        case "LessThanOrEqualTo":
                            ifstatement.SetCompareOperator(CompareOperator.LessThanOrEquals);
                            break;
                        case "GreaterThanOrEqualTo":
                            ifstatement.SetCompareOperator(CompareOperator.GreaterThanOrEquals);
                            break;
                        case "LessThan":
                            ifstatement.SetCompareOperator(CompareOperator.LessThan);
                            break;
                        case "GreaterThan":
                            ifstatement.SetCompareOperator(CompareOperator.GreaterThan);
                            break;
                        case "EqualTo":
                            ifstatement.SetCompareOperator(CompareOperator.Equals);
                            break;
                        case "NotEqualTo":
                            ifstatement.SetCompareOperator(CompareOperator.NotEquals);
                            break;
                        default:
                            Debug.LogError("NEW FUNCTION NAME: " + clause.expression.function.name);
                            break;
                    }

                    Flowchart currFlowchart = currentFlowchartObj.GetComponent<Flowchart>();

                    Parser.Expression secondExpression = clause.expression.parameters[1];
                    switch (secondExpression.value.value.type)
                    {
                        case Value.Type.String:
                            StringVariable sv = null;
                            if (currFlowchart.GetVariable<StringVariable>(clause.expression.parameters[0].value.value.GetVariableName()) == null)
                            {
                                sv = currentFlowchartObj.AddComponent<StringVariable>();
                                sv.Scope = VariableScope.Public;
                                sv.Key = clause.expression.parameters[0].value.value.GetVariableName();
                                sv.Value = "";
                                currFlowchart.AddVariable(sv);
                                currFlowchart.SetStringVariable(sv.Key, "");
                            }
                            else
                            {
                                sv = currFlowchart.GetVariable<StringVariable>(clause.expression.parameters[0].value.value.GetVariableName());
                            }
                            StringDataMulti data = new StringDataMulti();
                            data.stringVal = secondExpression.value.value.GetStringValue();
                            ifstatement.SetVariable(sv);
                            ifstatement.SetStringData(data);
                            break;
                        case Value.Type.Number:
                            FloatVariable fv = null;
                            if (currFlowchart.GetVariable<FloatVariable>(clause.expression.parameters[0].value.value.GetVariableName()) == null)
                            {
                                fv = currentFlowchartObj.AddComponent<FloatVariable>();
                                fv.Scope = VariableScope.Public;
                                fv.Key = clause.expression.parameters[0].value.value.GetVariableName();
                                fv.Value = 0;
                                currFlowchart.AddVariable(fv);
                                currFlowchart.SetFloatVariable(fv.Key, 0);
                            }
                            else
                            {
                                fv = currFlowchart.GetVariable<FloatVariable>(clause.expression.parameters[0].value.value.GetVariableName());
                            }
                            FloatData fdata = new FloatData();
                            fdata.floatVal = secondExpression.value.value.GetNumberValue();
                            ifstatement.SetVariable(fv);
                            ifstatement.SetFloatData(fdata);
                            break;
                        case Value.Type.Bool:
                            BooleanVariable bv = null;
                            if (currFlowchart.GetVariable<BooleanVariable>(clause.expression.parameters[0].value.value.GetVariableName()) == null)
                            {
                                bv = currentFlowchartObj.AddComponent<BooleanVariable>();
                                bv.Scope = VariableScope.Public;
                                bv.Key = clause.expression.parameters[0].value.value.GetVariableName();
                                bv.Value = false;
                                currFlowchart.AddVariable(bv);
                                currFlowchart.SetBooleanVariable(bv.Key, false);
                            }
                            else
                            {
                                bv = currFlowchart.GetVariable<BooleanVariable>(clause.expression.parameters[0].value.value.GetVariableName());
                            }
                            BooleanData bdata = new BooleanData();
                            bdata.booleanVal = secondExpression.value.value.GetBoolValue();
                            ifstatement.SetVariable(bv);
                            ifstatement.SetBooleanData(bdata);
                            break;
                        default:
                            Debug.LogError("Unknown Parser Value Type");
                            break;
                    }
                }
            }
            //otherwise, it's an elseif statement
            else
            {
                ElseIf ifstatement = currentFlowchartObj.AddComponent<ElseIf>();
                block.CommandList.Add(ifstatement);
                if (clause.expression.function != null)
                {
                    //it is an operator statement
                    switch (clause.expression.function.name)
                    {
                        case "LessThanOrEqualTo":
                            ifstatement.SetCompareOperator(CompareOperator.LessThanOrEquals);
                            break;
                        case "GreaterThanOrEqualTo":
                            ifstatement.SetCompareOperator(CompareOperator.GreaterThanOrEquals);
                            break;
                        case "LessThan":
                            ifstatement.SetCompareOperator(CompareOperator.LessThan);
                            break;
                        case "GreaterThan":
                            ifstatement.SetCompareOperator(CompareOperator.GreaterThan);
                            break;
                        case "EqualTo":
                            ifstatement.SetCompareOperator(CompareOperator.Equals);
                            break;
                        case "NotEqualTo":
                            ifstatement.SetCompareOperator(CompareOperator.NotEquals);
                            break;
                        default:
                            Debug.LogError("NEW FUNCTION NAME: " + clause.expression.function.name);
                            break;
                    }

                    Flowchart currFlowchart = currentFlowchartObj.GetComponent<Flowchart>();

                    Parser.Expression secondExpression = clause.expression.parameters[1];
                    switch (secondExpression.value.value.type)
                    {
                        case Value.Type.String:
                            StringVariable sv = null;
                            if (currFlowchart.GetVariable<StringVariable>(clause.expression.parameters[0].value.value.GetVariableName()) == null)
                            {
                                sv = currentFlowchartObj.AddComponent<StringVariable>();
                                sv.Scope = VariableScope.Public;
                                sv.Key = clause.expression.parameters[0].value.value.GetVariableName();
                                sv.Value = "";
                                currFlowchart.AddVariable(sv);
                                currFlowchart.SetStringVariable(sv.Key, "");
                            }
                            else
                            {
                                sv = currFlowchart.GetVariable<StringVariable>(clause.expression.parameters[0].value.value.GetVariableName());
                            }
                            StringDataMulti data = new StringDataMulti();
                            data.stringVal = secondExpression.value.value.GetStringValue();
                            ifstatement.SetVariable(sv);
                            ifstatement.SetStringData(data);
                            break;
                        case Value.Type.Number:
                            FloatVariable fv = null;
                            if (currFlowchart.GetVariable<FloatVariable>(clause.expression.parameters[0].value.value.GetVariableName()) == null)
                            {
                                fv = currentFlowchartObj.AddComponent<FloatVariable>();
                                fv.Scope = VariableScope.Public;
                                fv.Key = clause.expression.parameters[0].value.value.GetVariableName();
                                fv.Value = 0;
                                currFlowchart.AddVariable(fv);
                                currFlowchart.SetFloatVariable(fv.Key, 0);
                            }
                            else
                            {
                                fv = currFlowchart.GetVariable<FloatVariable>(clause.expression.parameters[0].value.value.GetVariableName());
                            }
                            FloatData fdata = new FloatData();
                            fdata.floatVal = secondExpression.value.value.GetNumberValue();
                            ifstatement.SetVariable(fv);
                            ifstatement.SetFloatData(fdata);
                            break;
                        case Value.Type.Bool:
                            BooleanVariable bv = null;
                            if (currFlowchart.GetVariable<BooleanVariable>(clause.expression.parameters[0].value.value.GetVariableName()) == null)
                            {
                                bv = currentFlowchartObj.AddComponent<BooleanVariable>();
                                bv.Scope = VariableScope.Public;
                                bv.Key = clause.expression.parameters[0].value.value.GetVariableName();
                                bv.Value = false;
                                currFlowchart.AddVariable(bv);
                                currFlowchart.SetBooleanVariable(bv.Key, false);
                            }
                            else
                            {
                                bv = currFlowchart.GetVariable<BooleanVariable>(clause.expression.parameters[0].value.value.GetVariableName());
                            }
                            BooleanData bdata = new BooleanData();
                            bdata.booleanVal = secondExpression.value.value.GetBoolValue();
                            ifstatement.SetVariable(bv);
                            ifstatement.SetBooleanData(bdata);
                            break;
                        default:
                            Debug.LogError("Unknown Parser Value Type");
                            break;
                    }
                }
            }

            //Parse the statements once you figure out what kind of if-else to use
            ParseNodeHelper(clause.statements, block, nodes, blocks);
        }

        //Put in an end-if
        End end = currentFlowchartObj.AddComponent<End>();
        block.CommandList.Add(end);
    }

    public void CreateOptionGroup(List<Parser.ShortcutOption> options, Block block, Dictionary<string, Yarn.Parser.Node> nodes, Dictionary<string, Block> blocks)
    {
        foreach(Parser.ShortcutOption option in options)
        {
            //make menu option and set target option to a new block containing the same label as the menu button
            Menu menu = currentFlowchartObj.AddComponent<Menu>();
            menu.SetText(option.label);
            block.CommandList.Add(menu);
            Block b = currentFlowchartObj.AddComponent<Block>();
            b.BlockName = option.label;
            blocks[option.label] = b;
            menu.SetTargetBlock(b);
            ParseNodeHelper(option.optionNode, b, nodes, blocks);
        }
    }

    public void CreateSay(string line, Block block)
    {
        Say s = currentFlowchartObj.AddComponent<Say>();
        s.SetStandardText(line);
        block.CommandList.Add(s);
    }

    public void CreateCallCommand(string dest, Block block, Dictionary<string, Block> blocks)
    {
        Call call = currentFlowchartObj.AddComponent<Call>();
        if (!blocks.ContainsKey(dest))
        {
            Block b = currentFlowchartObj.AddComponent<Block>();
            b.BlockName = dest;
            blocks[dest] = b;
            call.SetTargetBlock(b);
            block.CommandList.Add(call);
        }
        else
        {
            call.SetTargetBlock(blocks[dest]);
            block.CommandList.Add(call);
        }
        
    }

    public void CreateCustomCommand(string command, Block block)
    {
        string[] commandTokens = command.Split(null);
        if (commandTokens != null)
        {
            switch (commandTokens[0])
            {
                case "wait":
                    string secondsString = commandTokens[1];
                    secondsString = secondsString.Replace("s", "");
                    int seconds = int.Parse(secondsString);
                    Wait w = currentFlowchartObj.AddComponent<Wait>();
                    w.SetDuration(new FloatData(seconds));
                    block.CommandList.Add(w);
                    break;
            }
        }
    }
    #endregion

    #region DIALOGUEUI OVERRIDES
    public override IEnumerator RunLine(Line line)
    {
        yield return null;
    }

    public override IEnumerator RunCommand(Yarn.Command command)
    {
        yield return null;
    }

    public override IEnumerator RunOptions(Options optionsCollection, OptionChooser optionChooser)
    {
        yield return null;
    }
    #endregion
}
