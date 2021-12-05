import React from 'react';
import {
    Accordion,
    Alert,
    Badge,
    Card,
    ListGroup,
    Row,
    Col,
    Button,
    Form
} from "react-bootstrap";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";

import {
	faTrashAlt,
	faSitemap,
	faListUl,
	faChevronDown,
  faPlusCircle
} from "@fortawesome/free-solid-svg-icons";

class TaskList2 extends React.Component{
    render() {
        return (
          <>
          <Row>
            <Col>
            <Accordion defaultActiveKey="0">
          <Card className="border-0">
            <Accordion.Toggle as={Card.Header} eventKey="0">
            <div className="d-flex justify-content-between">
              <span>
              <FontAwesomeIcon icon={faChevronDown} className="mr-2" /> Today's Task
              </span>
              <span>
              <FontAwesomeIcon icon={faListUl} className="mr-2" /> 3 Task
              <Badge className="ml-3" pill variant="info">
                In-Progress
              </Badge>
              </span>
            </div>
            </Accordion.Toggle>
            <Accordion.Collapse eventKey="0">
              <Card.Body className="pt-0">
              <ListGroup>
                <ListGroup.Item className="d-flex justify-content-between align-items-start">
                  Opening Checks- Food Safety
                  <div className="d-flex align-items-center">
                    <Alert className="alert-sm mb-0 mr-3" variant="success">
                      Published
                    </Alert>
                    
                    <FontAwesomeIcon icon={faSitemap}/>
                    <Badge className="ml-2" pill variant="light">
                      8
                    </Badge>
                  </div>
                  
                </ListGroup.Item>
                <ListGroup.Item className="d-flex justify-content-between align-items-start">
                  Opening Checks- Food Safety
                  <div className="d-flex align-items-center">
                    <Alert className="alert-sm mb-0 mr-3" variant="success">
                      Published
                    </Alert>
                    
                    <FontAwesomeIcon icon={faSitemap}/>
                    <Badge className="ml-2" pill variant="light">
                      8
                    </Badge>
                  </div>
                  
                </ListGroup.Item>
                <ListGroup.Item className="d-flex justify-content-between align-items-start">
                  Opening Checks- Food Safety
                  <div className="d-flex align-items-center">
                    <Alert className="alert-sm mb-0 mr-3" variant="success">
                      Published
                    </Alert>
                    
                    <FontAwesomeIcon icon={faSitemap}/>
                    <Badge className="ml-2" pill variant="light">
                      8
                    </Badge>
                  </div>
                  
                </ListGroup.Item>
              </ListGroup>
              </Card.Body>
            </Accordion.Collapse>
          </Card>
          <Card className="border-0">
            <Accordion.Toggle as={Card.Header} eventKey="1">
            <div className="d-flex justify-content-between">
              <span>
              <FontAwesomeIcon icon={faChevronDown} className="mr-2" /> 05/12/2021
              </span>
              <span>
              <FontAwesomeIcon icon={faListUl} className="mr-2" /> 3 Task
              <Badge className="ml-3" pill variant="success">
                Completed
              </Badge>{' '}
              </span>
            </div>
            </Accordion.Toggle>
            <Accordion.Collapse eventKey="1">
              <Card.Body className="pt-0">
              <ListGroup>
                <ListGroup.Item className="d-flex justify-content-between align-items-start">
                  Opening Checks- Food Safety
                  <div className="d-flex align-items-center">
                    <Alert className="alert-sm mb-0 mr-3" variant="success">
                      Published
                    </Alert>
                    
                    <FontAwesomeIcon icon={faSitemap}/>
                    <Badge className="ml-2" pill variant="light">
                      8
                    </Badge>
                  </div>
                  
                </ListGroup.Item>
                <ListGroup.Item className="d-flex justify-content-between align-items-start">
                  Opening Checks- Food Safety
                  <div className="d-flex align-items-center">
                    <Alert className="alert-sm mb-0 mr-3" variant="success">
                      Published
                    </Alert>
                    
                    <FontAwesomeIcon icon={faSitemap}/>
                    <Badge className="ml-2" pill variant="light">
                      8
                    </Badge>
                  </div>
                  
                </ListGroup.Item>
                <ListGroup.Item className="d-flex justify-content-between align-items-start">
                  Opening Checks- Food Safety
                  <div className="d-flex align-items-center">
                    <Alert className="alert-sm mb-0 mr-3" variant="success">
                      Published
                    </Alert>
                    
                    <FontAwesomeIcon icon={faSitemap}/>
                    <Badge className="ml-2" pill variant="light">
                      8
                    </Badge>
                  </div>
                  
                </ListGroup.Item>
              </ListGroup>
              </Card.Body>
            </Accordion.Collapse>
          </Card>
        </Accordion>
            </Col>
            <Col>
            <header className="d-flex justify-content-between align-items-center mb-3">
            <h5 className="font-weight-bold mb-0">Opening Checks- Food Safety</h5>
                  <Form.Group controlId="exampleForm.ControlSelect1" className="mb-0">
                    <Form.Control size="sm" as="select" className="bg-success text-white">
                      <option>Published</option>
                      <option>Not Published</option>
                      <option>Draft</option>
                    </Form.Control>
                  </Form.Group>
                  
                </header>
                <p>
                Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry’s standard dummy text ever since the 1500s,
                </p>
                
                <div className="d-flex justify-content-between">
                <h6 className="font-weight-bold">Subtask</h6>
                <Button variant="link"><FontAwesomeIcon icon={faPlusCircle}/> Add Subtask</Button>
                  </div>
                  <Form>
                    <Form.Row className="align-items-center">
                      <Col>
                        <Form.Label htmlFor="inlineFormInput" srOnly>
                          Name
                        </Form.Label>
                        <Form.Control
                          className="mb-2"
                          id="inlineFormInput"
                          placeholder="Enter task name"
                        />
                      </Col>
                      <Col xs="auto">
                        <Button type="submit" className="mb-2">
                          Add Task
                        </Button>
                      </Col>
                    </Form.Row>
                  </Form>
                <ListGroup variant="flush">
                <ListGroup.Item className="d-flex justify-content-between align-items-start pl-0">
                <Form>
                <Form.Check
                  type="checkbox"
                  id="autoSizingCheck"
                  className="mb-2"
                  label="Sub Task title goes here"
                />
                </Form>
                <Button size="sm" variant="link">
                    <FontAwesomeIcon icon={faTrashAlt}/>
                  </Button>
                </ListGroup.Item>
                <ListGroup.Item className="d-flex justify-content-between align-items-start pl-0">
                <Form>
                <Form.Check
                  type="checkbox"
                  id="autoSizingCheck"
                  className="mb-2"
                  label="Sub Task title goes here"
                />
                </Form>
                <Button size="sm" variant="link">
                    <FontAwesomeIcon icon={faTrashAlt}/>
                  </Button>
                </ListGroup.Item>
                <ListGroup.Item className="d-flex justify-content-between align-items-start pl-0">
                <Form>
                <Form.Check
                  type="checkbox"
                  id="autoSizingCheck"
                  className="mb-2"
                  label="Sub Task title goes here"
                />
                </Form>
                <Button size="sm" variant="link">
                    <FontAwesomeIcon icon={faTrashAlt}/>
                  </Button>
                </ListGroup.Item>
              </ListGroup>
            </Col>
          </Row>
          
        </>
         );
    }
    

};

export default TaskList2;
