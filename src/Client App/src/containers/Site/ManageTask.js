import React, { useState, useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { getSiteById } from '../../store/site-actions';
import {
	Container,
	Col,
	Row,
	Spinner,
	Button,
	Table,
    Badge,
    Accordion,
	Breadcrumb,
	Dropdown,
	InputGroup,
	FormControl,
} from "react-bootstrap";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
	faPencilAlt,
	faEllipsisV,
	faTrash,
	faTrashRestore,
	faSearch,
	faPlusCircle,
} from "@fortawesome/free-solid-svg-icons";
import DatePicker from "react-datepicker";
import { getCalendar } from '../../store/calendar-actions';
import TaskList2 from '../../components/TaskList2/TaskList2';
import AssignTask from './AssignTask';

import _ from 'lodash';
import { succeeded } from '../../utils/stateStatus';

const ManageTask = ({ match }) => {

    const [startDate, setStartDate] = useState(new Date());
    const [showAssignTaskModal, setShowAssignTaskModal] = useState(false);

    const { status: calendarStatus, calendarInfo } = useSelector(state => state.calendar);
    const { status: siteStatus, siteById } = useSelector(state => state.site)

    const assignTaskHandler = () => {
        setShowAssignTaskModal(true);
    }

    const assignTaskModalCloseHandler = () => {
        setShowAssignTaskModal(false);
    }

    const dispatch = useDispatch();

    useEffect(() => {

        dispatch(getSiteById(match.params.id))

    }, [dispatch, match.params.id])

    useEffect(() => {

        dispatch(getCalendar(match.params.id, startDate.toISOString().split('T')[0]))

    }, [dispatch, match.params.id, startDate])

    const orderByDueDate = () => {
        let orderedData = _.orderBy(calendarInfo.data.taskList, ['dueBy'], ['asc'])
        return orderedData;
    }


    return (
        <>
            {
                siteStatus === succeeded ?
                    (!_.isEmpty(siteById) && siteById.id === match.params.id) ? (
                        
                        <div>
                            <section className="site-breadcrumb">
						<Container className="text-center">
							<Row>
								<Col>
									<h5 className="mb-0">{siteById.name}</h5>
									<Breadcrumb className="justify-content-center">
										<Breadcrumb.Item>Home</Breadcrumb.Item>
                                        <Breadcrumb.Item>Sites</Breadcrumb.Item>
										<Breadcrumb.Item active>{siteById.name}</Breadcrumb.Item>
									</Breadcrumb>
								</Col>
							</Row>
						</Container>
					</section>
                    <section>
						<Container>
							<Row>
								<Col>
									<div className="table-container bg-white" style={{marginTop:"-52.5px"}}>
										<header className="table-cta bg-light p-2 clearfix">
                                        <DatePicker
                                        selected={startDate}
                                        onChange={date => setStartDate(date)}
                                        maxDate={new Date()}
                                        todayButton="Today"
                                        className="form-control"
                                    />
                                            <Button
												type="button"
												variant="primary"
												className="float-right"
											     onClick={assignTaskHandler}
                                                 >
                                                 Assign Task
                                                 </Button>
                                            
                                        </header>
                                        <div className="p-3">
                                        <TaskList2 />
                                        
                                        {/* {
                                                calendarStatus === succeeded ?
                                                    !_.isEmpty(calendarInfo.data) ?
                                                        <Accordion defaultActiveKey="0">

                                                            {orderByDueDate().map((taskListItem, i) => {
                                                                return ( */}
                                                                    {/* <> */}
                                                                        {/* <TaskList data={taskListItem} index={i.toString()} calendarId={calendarInfo.data.id} /> */}
                                                                        
                                                                    {/* </> */}
                                                                {/* )
                                                            })}
                                                        </Accordion>
                                                        : <h4 className="text-muted center">No Tasks</h4>
                                                    : <Spinner animation="border spinner" />

                                            } */}
                                        </div>
                                           
                                    </div>
                                </Col>
                            </Row>
                        </Container>
                    </section>
                            {showAssignTaskModal ? <AssignTask show={showAssignTaskModal} onClose={assignTaskModalCloseHandler} data={match.params.id} /> : ''}


                        </div>
                    ) : <Spinner animation="border spinner" /> 
                    
                    : <Spinner animation="border spinner" />
                }
        </>
    )
}

export default ManageTask
