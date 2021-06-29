import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useDispatch, useSelector } from 'react-redux';
import { Container, Col, Row, Spinner, Button, Breadcrumb, InputGroup, Form } from 'react-bootstrap';
import { getSite, getSiteManager } from '../../store/site-actions';

import ReactDataGrid from '@inovua/reactdatagrid-community';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faCheck, faTimes, faEdit, faTrash, faSearch } from '@fortawesome/free-solid-svg-icons'

import '../../App.scss';
import EditSite from './EditSite';
import AddSite from './AddSite';
import DeleteSite from './DeleteSite';

import _ from 'lodash';
import { idle, succeeded } from '../../utils/stateStatus';
import { returnLoggedInUser } from '../../store/auth-actions';

const gridStyle = { minHeight: 485, marginTop: 10 };


const Site = () => {

    const dispatch = useDispatch();

    const user  = dispatch(returnLoggedInUser());

    const columns = [

        {
            name: 'name', defaultFlex: 1, header: 'Name',
            render: ({ data }) => <Link to={`/site/${data.id}`}>{data.name}</Link>

        },
        { name: 'scoreValue', defaultFlex: 1, header: 'Average Score' },
        {
            name: 'lastSeen', defaultFlex: 1, header: 'Last Seen',
            render: ({ value }) => {
                let date, time;
                [date, time] = value.split('T')
                return (
                    <>
                        <span>{date}</span>
                    </>
                )
            }
        },
        {
            name: 'isActive', defaultFlex: 0.5, header: 'Active', className: "text-center",
            render: ({ value }) => <div className="text-center"> {value ?
                <FontAwesomeIcon icon={faCheck} className="text-success" />
                : <FontAwesomeIcon icon={faTimes} className="text-danger" />} </div>
        },
        {
            header: 'Edit',
            name: 'id',
            defaultFlex: 0.5,
            className: "text-center",
            render: ({ data }) => {
                return (
                    <div className="text-center">
                        <Button variant="dark" size="sm" block onClick={() => editHandler(data)}>
                            <FontAwesomeIcon icon={faEdit} className="text-light" />
                        </Button>
                    </div>
                )
            }
        }       

    ];

    user.role === "Admin" && columns.push({

        header: 'Delete',
        defaultFlex: 0.5,
        className: "text-center",
        render: ({ data }) => {
            return (
                <div className="text-center">
                    <Button variant="dark" size="sm" block onClick={() => { deleteHandler(data) }}>
                        <FontAwesomeIcon icon={faTrash} className="text-light" />
                    </Button>
                </div>
            )
        }

    })

    const { status, siteInfo, recentGETMethod } = useSelector(state => state.site);
    const { companyId } = useSelector(state => state.auth.user);
    const [skip, setSkip] = useState(0);

    const [showEditModal, setShowEditModal] = useState(false);
    const [showAddModal, setShowAddModal] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);

    const [editData, setEditData] = useState({});
    const [deleteData, setDeleteData] = useState({});

    

    const addHandler = () => {

        setShowAddModal(true);
    }

    const editHandler = (data) => {
        console.log(data)
        setEditData(data);
        setShowEditModal(true);
    }

    const deleteHandler = (data) => {

        setDeleteData(data);
        setShowDeleteModal(true);
    }

    const addModalCloseHandler = () => {
        setShowAddModal(false);
    }

    const editModalCloseHandler = () => {
        setShowEditModal(false);
    }

    const deleteModalCloseHandler = () => {
        setShowDeleteModal(false);
    }

    const skipChangeHandler = (skipValue) => {

        const pageNumberValue = (skipValue / 10) + 1;
        user.role === "Admin" ? 
         dispatch(getSite(companyId, { pageNumber: pageNumberValue, pageSize: 10 })) : 
         dispatch(getSiteManager({ pageNumber: pageNumberValue, pageSize: 10 }))
        setSkip(skipValue);
    }

    useEffect(() => {
       
        if(status === idle || (status === succeeded && recentGETMethod !== "getSite")) {
            if(user.role === "Admin"){
                dispatch(getSite(companyId)); 
            }
            else{
                dispatch(getSiteManager())
            }
        }       
       
    }, [dispatch, companyId, status, recentGETMethod, user.role])

    return (
        <>
        <section className="site-breadcrumb bg-light">
                        <Container className="text-center">
                        <Row>
                            <Col>
                            <h5>Site</h5>
                            <Breadcrumb className="justify-content-center">
                            <Breadcrumb.Item href="#">Home</Breadcrumb.Item>
                            <Breadcrumb.Item active>Site</Breadcrumb.Item>
                        </Breadcrumb>
                            </Col>
                            
                    
                        </Row>
                        </Container>
                    </section>
                    <section>
                    <Container>
                        <Row>
                            <Col>
                            <div className="table-container bg-white">
                                    <header className="table-cta p-2 clearfix">
                                    <InputGroup className="w-25 float-left">
                                        <InputGroup.Text id="basic-addon1"><FontAwesomeIcon icon={faSearch}/> </InputGroup.Text>
                                        <Form.Control
                                        placeholder="Search"
                                        aria-label="Search"
                                        aria-describedby="basic-addon1"
                                        />
                                    </InputGroup>
                                       
                                    {
                                    user.role === 'Admin' && 
                                            <Button type="button" variant="primary" className='float-right' onClick={() => { addHandler() }}>Add Site</Button>
                                       
                                    }
                                    </header>
                                    <AddSite show={showAddModal} onClose={addModalCloseHandler} skip={skip} />
            {showEditModal ? <EditSite show={showEditModal} onClose={editModalCloseHandler} data={editData} /> : ''}
            {showDeleteModal ? <DeleteSite show={showDeleteModal} onClose={deleteModalCloseHandler} data={deleteData} /> : ''}
         
                {
                    status === succeeded ?
                        !_.isEmpty(siteInfo)
                            ? <ReactDataGrid
                                idProperty="id"
                                style={gridStyle}
                                columns={columns}
                                pagination
                                skip={skip}
                                sortable={false}
                                showColumnMenuTool={false}
                                onSkipChange={skipChangeHandler}
                                pageSizes={false}
                                theme="default-light"
                                dataSource={new Promise(function (resolve, reject) {
                                    resolve(siteInfo)
                                })}
                                defaultLimit={10}
                            />

                            : <h4 className="text-muted center">No Data</h4>
                        : <Spinner animation="border spinner" />
                }
                            </div>

                            </Col>
                        </Row>
                    </Container>
                    </section>
            
           
        </>
    )



};

export default Site;